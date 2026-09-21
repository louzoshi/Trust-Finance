using System.Globalization;
using System.Text;

namespace TrustFinance.Domain.Banking;

/// <summary>A Pix QR code, decoded: who is being paid, how much, and the transaction id.</summary>
public sealed record PixInfo(
    string Key,
    string MerchantName,
    string MerchantCity,
    decimal? Amount,
    string? Description,
    string? TransactionId,
    bool IsDynamic);

/// <summary>
/// Decodes the BR Code — the EMV® QR payload behind every Pix QR code and "Pix copia e
/// cola" string — as specified in the Banco Central's Manual de Padrões para
/// Iniciação do Pix.
///
/// The payload is a flat list of tag-length-value fields, two digits each for tag and
/// length. Field 26 nests the Pix account: its GUI must be "br.gov.bcb.pix", then the
/// key (static code) or the payload URL (dynamic code). Field 54 is the amount, 59 and
/// 60 the merchant, 62 the transaction id, and 63 a CRC-16/CCITT-FALSE over everything
/// up to and including its own "6304" header. A payload whose CRC does not match is
/// refused: a single changed digit in a pasted code is exactly the kind of thing the
/// checksum exists to catch.
/// </summary>
public static class PixBrCode
{
    private const string PixGui = "br.gov.bcb.pix";

    public static bool TryParse(string? input, out PixInfo info, out string error)
    {
        info = null!;
        error = string.Empty;

        var payload = (input ?? string.Empty).Trim();
        if (payload.Length < 8 || !payload.StartsWith("000201", StringComparison.Ordinal))
        {
            error = "Isto não parece um código Pix: ele começa com 000201.";
            return false;
        }

        var crcAt = payload.LastIndexOf("6304", StringComparison.Ordinal);
        if (crcAt < 0 || crcAt + 8 != payload.Length)
        {
            error = "O código Pix não termina com o campo de verificação (6304).";
            return false;
        }

        var expected = payload[(crcAt + 4)..];
        var actual = Crc16(payload[..(crcAt + 4)]).ToString("X4", CultureInfo.InvariantCulture);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            error = "O código Pix está corrompido: a verificação não confere.";
            return false;
        }

        Dictionary<string, string> fields;
        try
        {
            fields = ReadTlv(payload);
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }

        if (!fields.TryGetValue("26", out var account))
        {
            error = "O código não traz uma conta Pix (campo 26).";
            return false;
        }

        var accountFields = ReadTlv(account);
        if (!accountFields.TryGetValue("00", out var gui) || !string.Equals(gui, PixGui, StringComparison.OrdinalIgnoreCase))
        {
            error = "O código é um QR EMV, mas não é Pix.";
            return false;
        }

        // A static code carries the key in 01; a dynamic one carries a URL in 25 instead.
        var isDynamic = accountFields.ContainsKey("25") && !accountFields.ContainsKey("01");
        var key = accountFields.GetValueOrDefault("01") ?? accountFields.GetValueOrDefault("25") ?? string.Empty;

        decimal? amount = null;
        if (fields.TryGetValue("54", out var raw) &&
            decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
            amount = parsed;

        string? txid = null;
        if (fields.TryGetValue("62", out var additional))
        {
            var extra = ReadTlv(additional);
            txid = extra.GetValueOrDefault("05");
            if (txid == "***") txid = null;
        }

        info = new PixInfo(
            key,
            fields.GetValueOrDefault("59") ?? string.Empty,
            fields.GetValueOrDefault("60") ?? string.Empty,
            amount,
            accountFields.GetValueOrDefault("02"),
            txid,
            isDynamic);
        return true;
    }

    private static Dictionary<string, string> ReadTlv(string payload)
    {
        var fields = new Dictionary<string, string>();
        var i = 0;

        while (i < payload.Length)
        {
            if (i + 4 > payload.Length)
                throw new FormatException("O código Pix termina no meio de um campo.");

            var tag = payload.Substring(i, 2);
            if (!int.TryParse(payload.AsSpan(i + 2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var length))
                throw new FormatException("O código Pix tem um tamanho de campo inválido.");

            i += 4;
            if (i + length > payload.Length)
                throw new FormatException("O código Pix declara um campo maior do que o conteúdo.");

            fields[tag] = payload.Substring(i, length);
            i += length;
        }

        return fields;
    }

    /// <summary>CRC-16/CCITT-FALSE: polynomial 0x1021, initial 0xFFFF, no reflection, no final xor.</summary>
    public static ushort Crc16(string text)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            crc ^= (ushort)(b << 8);
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }
}
