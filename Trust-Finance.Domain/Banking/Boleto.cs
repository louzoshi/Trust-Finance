using System.Globalization;

namespace TrustFinance.Domain.Banking;

public enum BoletoKind
{
    /// <summary>Cobrança bancária: 47 digits, a bank code up front, value and due date at the end.</summary>
    Bank = 1,

    /// <summary>Arrecadação (utilities, taxes): 48 digits starting with 8, value in the first block.</summary>
    Collection = 2
}

/// <summary>What the typed line says, once its check digits have been believed.</summary>
public sealed record BoletoInfo(
    BoletoKind Kind,
    string Digits,
    decimal? Amount,
    DateOnly? DueDate,
    string? BankCode,
    string? Segment);

/// <summary>
/// Decodes the "linha digitável" printed on a Brazilian boleto — the 47 or 48 digits a
/// person types to pay one — enough to know what it is for, how much and by when,
/// and to reject a mistyped one before it reaches the ledger.
///
/// Rules implemented, as published by FEBRABAN:
///
/// - A bank boleto has three fields checked by modulo 10, a general check digit by
///   modulo 11 over the 44-digit barcode, then a due-date factor and the value.
/// - The due-date factor counts days from 7 October 1997. It ran past 9999 on
///   21 February 2025 and restarted at 1000 the next day; a factor is read on the
///   cycle that puts it nearest to today.
/// - A collection slip's four blocks each carry their own check digit, modulo 10 or
///   modulo 11 depending on the third digit, and the value sits right after the
///   first four digits.
/// </summary>
public static class Boleto
{
    private static readonly DateOnly FactorBase = new(1997, 10, 7);
    private static readonly DateOnly FactorRollover = new(2025, 2, 22); // factor 1000 again

    public static bool TryParse(string? input, DateOnly today, out BoletoInfo info, out string error)
    {
        info = null!;
        error = string.Empty;

        var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());

        switch (digits.Length)
        {
            case 47:
                return TryParseBank(digits, today, out info, out error);
            case 48 when digits[0] == '8':
                return TryParseCollection(digits, out info, out error);
            default:
                error = "Uma linha digitável tem 47 dígitos (cobrança) ou 48 começando com 8 (arrecadação).";
                return false;
        }
    }

    private static bool TryParseBank(string d, DateOnly today, out BoletoInfo info, out string error)
    {
        info = null!;

        // Fields: 1 = d[0..9] + dv, 2 = d[10..20] + dv, 3 = d[21..31] + dv, 4 = general dv, 5 = factor + value.
        if (Mod10(d[..9]) != d[9] - '0' || Mod10(d[10..20]) != d[20] - '0' || Mod10(d[21..31]) != d[31] - '0')
        {
            error = "Um dos campos da linha digitável não confere — confira os dígitos.";
            return false;
        }

        // The 44-digit barcode is the fields rearranged; its own check digit is the 5th
        // position of the barcode and the 33rd digit of the typed line.
        var bankAndCurrency = d[..4];
        var generalDv = d[32];
        var factorAndValue = d[33..47];
        var freeField = d[4..9] + d[10..20] + d[21..31];
        var barcode = bankAndCurrency + generalDv + factorAndValue + freeField;

        if (Mod11Barcode(barcode.Remove(4, 1)) != generalDv - '0')
        {
            error = "O dígito verificador geral não confere.";
            return false;
        }

        var factor = int.Parse(factorAndValue[..4], CultureInfo.InvariantCulture);
        var cents = long.Parse(factorAndValue[4..], CultureInfo.InvariantCulture);

        info = new BoletoInfo(
            BoletoKind.Bank,
            d,
            cents > 0 ? cents / 100m : null,
            factor > 0 ? DueDateFromFactor(factor, today) : null,
            BankCode: d[..3],
            Segment: null);
        error = string.Empty;
        return true;
    }

    private static bool TryParseCollection(string d, out BoletoInfo info, out string error)
    {
        info = null!;

        // Four blocks of 11 digits + 1 check digit. The third digit says which modulo.
        var useMod10 = d[2] is '6' or '7';

        for (var b = 0; b < 4; b++)
        {
            var block = d.Substring(b * 12, 11);
            var dv = d[b * 12 + 11] - '0';
            var expected = useMod10 ? Mod10(block) : Mod11Collection(block);
            if (expected != dv)
            {
                error = $"O bloco {b + 1} da linha digitável não confere.";
                return false;
            }
        }

        // The barcode is the four blocks without their check digits: 44 digits.
        var barcode = string.Concat(Enumerable.Range(0, 4).Select(b => d.Substring(b * 12, 11)));
        var valueDigits = barcode[4..15];
        var cents = long.Parse(valueDigits, CultureInfo.InvariantCulture);

        // Digit 3 of 6/8 means "value is the amount"; 7/9 means it is a reference, not money.
        var isAmount = d[2] is '6' or '8';

        info = new BoletoInfo(
            BoletoKind.Collection,
            d,
            isAmount && cents > 0 ? cents / 100m : null,
            DueDate: null,
            BankCode: null,
            Segment: SegmentName(d[1]));
        error = string.Empty;
        return true;
    }

    /// <summary>The due date the factor encodes, on whichever cycle lands closest to today.</summary>
    public static DateOnly DueDateFromFactor(int factor, DateOnly today)
    {
        var first = FactorBase.AddDays(factor);
        var second = FactorRollover.AddDays(factor - 1000);

        // Before the rollover only the first cycle exists; after it, pick the nearer.
        if (today < FactorRollover)
            return first;

        return Math.Abs(second.DayNumber - today.DayNumber) <= Math.Abs(first.DayNumber - today.DayNumber) ? second : first;
    }

    /// <summary>Modulo 10, right to left, weights 2 and 1, digit sums of products over 9.</summary>
    public static int Mod10(string digits)
    {
        var sum = 0;
        var weight = 2;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var product = (digits[i] - '0') * weight;
            sum += product > 9 ? product - 9 : product;
            weight = weight == 2 ? 1 : 2;
        }
        var remainder = sum % 10;
        return remainder == 0 ? 0 : 10 - remainder;
    }

    /// <summary>Modulo 11 for the barcode's general digit: weights 2..9 cycling; results 0, 10 and 11 become 1.</summary>
    public static int Mod11Barcode(string digits)
    {
        var sum = 0;
        var weight = 2;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            sum += (digits[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }
        var dv = 11 - sum % 11;
        return dv is 0 or 10 or 11 ? 1 : dv;
    }

    /// <summary>Modulo 11 for collection slips: same weights, but 10 becomes 0 and 11 becomes 0.</summary>
    public static int Mod11Collection(string digits)
    {
        var sum = 0;
        var weight = 2;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            sum += (digits[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }
        var remainder = sum % 11;
        return remainder is 0 or 1 ? 0 : 11 - remainder;
    }

    private static string SegmentName(char segment) => segment switch
    {
        '1' => "Prefeitura",
        '2' => "Saneamento",
        '3' => "Energia e gás",
        '4' => "Telecomunicações",
        '5' => "Órgão governamental",
        '6' => "Carnê / outros",
        '7' => "Multa de trânsito",
        '9' => "Uso exclusivo do banco",
        _ => "Arrecadação"
    };
}
