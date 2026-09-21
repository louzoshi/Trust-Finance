using System.Globalization;
using System.Text.RegularExpressions;

namespace TrustFinance.Domain.Banking;

/// <summary>One line of a bank or card statement, as the bank describes it.</summary>
public sealed record OfxTransaction(
    string FitId,
    DateOnly Date,
    decimal Amount,
    string Memo,
    string Type)
{
    public bool IsCredit => Amount > 0;
}

public sealed record OfxStatement(
    string? BankId,
    string? AccountId,
    bool IsCreditCard,
    DateOnly? From,
    DateOnly? To,
    decimal? LedgerBalance,
    IReadOnlyList<OfxTransaction> Transactions);

/// <summary>
/// Reads the OFX files Brazilian banks export. Most of them are OFX 1.x, which is
/// SGML: tags need not be closed and the header is plain text; some are the XML 2.x
/// flavour. A tolerant tag scanner handles both without a real parser for either.
///
/// Only what a personal ledger needs is read: the account, the period and the lines.
/// Each line's FITID is the bank's own identifier and travels with the transaction —
/// it is what makes importing the same file twice a no-op.
/// </summary>

public static partial class Ofx
{
    public static OfxStatement Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new FormatException("O arquivo está vazio.");

        var body = StripHeader(content);

        // Tag-by-tag scan. An element is "<TAG>value" up to the next tag; a closing
        // "</TAG>" carries no value. That covers SGML and XML alike.
        var tokens = Tokenize(body);

        var isCard = tokens.Any(t => t.Name == "CCSTMTRS");
        var transactions = new List<OfxTransaction>();

        string? bankId = null, accountId = null;
        DateOnly? from = null, to = null;
        decimal? balance = null;

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];

            // In the XML flavour every element is closed, and the closing tag carries no
            // value; reading it would blank the field the opening tag just filled.
            if (t.Closing && t.Name != "STMTTRN")
                continue;

            switch (t.Name)
            {
                case "BANKID": bankId ??= t.Value; break;
                case "ACCTID": accountId ??= t.Value; break;
                case "DTSTART": from ??= ParseDate(t.Value); break;
                case "DTEND": to ??= ParseDate(t.Value); break;
                case "BALAMT": balance ??= ParseAmount(t.Value); break;
                case "STMTTRN" when !t.Closing:
                    var (line, next) = ReadTransaction(tokens, i + 1);
                    if (line is not null) transactions.Add(line);
                    i = next - 1;
                    break;
            }
        }

        if (transactions.Count == 0 && !tokens.Any(t => t.Name is "BANKTRANLIST" or "STMTRS" or "CCSTMTRS"))
            throw new FormatException("Isto não parece um extrato OFX.");

        return new OfxStatement(bankId, accountId, isCard, from, to, balance, transactions);
    }

    private static (OfxTransaction? Line, int Next) ReadTransaction(List<(string Name, string Value, bool Closing)> tokens, int start)
    {
        string? fitId = null, memo = null, name = null, type = null;
        DateOnly? date = null;
        decimal? amount = null;

        var i = start;
        for (; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Name == "STMTTRN")
            {
                // Closing tag of this one, or the opening of the next in a file that
                // never closes anything — either way this line is done.
                if (t.Closing) i++;
                break;
            }

            if (t.Closing)
                continue;

            switch (t.Name)
            {
                case "FITID": fitId = t.Value; break;
                case "DTPOSTED": date = ParseDate(t.Value); break;
                case "TRNAMT": amount = ParseAmount(t.Value); break;
                case "MEMO": memo = t.Value; break;
                case "NAME": name = t.Value; break;
                case "TRNTYPE": type = t.Value; break;
            }
        }

        if (fitId is null || date is null || amount is null)
            return (null, i);

        // Banks put the useful text in MEMO or NAME depending on who wrote the export.
        var text = string.IsNullOrWhiteSpace(memo) ? name : memo;
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(memo) && !memo.Contains(name, StringComparison.OrdinalIgnoreCase))
            text = $"{name} {memo}";

        return (new OfxTransaction(fitId.Trim(), date.Value, amount.Value, Clean(text), type ?? "OTHER"), i);
    }

    private static List<(string Name, string Value, bool Closing)> Tokenize(string body)
    {
        var tokens = new List<(string, string, bool)>();
        var matches = TagPattern().Matches(body);

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var closing = m.Groups[1].Value == "/";
            var name = m.Groups[2].Value.ToUpperInvariant();

            var valueStart = m.Index + m.Length;
            var valueEnd = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            var value = body[valueStart..valueEnd].Trim();

            tokens.Add((name, closing ? string.Empty : value, closing));
        }

        return tokens;
    }

    private static string StripHeader(string content)
    {
        // SGML files open with "OFXHEADER:100" lines; XML ones with "<?xml". Both
        // end their preamble where the <OFX> element starts.
        var at = content.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        return at >= 0 ? content[at..] : content;
    }

    /// <summary>OFX dates are YYYYMMDD with optional time and a "[-3:BRT]" zone suffix.</summary>
    public static DateOnly? ParseDate(string raw)
    {
        var digits = new string(raw.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length < 8)
            return null;

        return DateOnly.TryParseExact(digits[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>Amounts come as "-123.45" but some exports use a comma; both are accepted.</summary>
    public static decimal? ParseAmount(string raw)
    {
        var normalized = raw.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Sem descrição";

        var collapsed = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length > 100 ? collapsed[..100] : collapsed;
    }

    [GeneratedRegex(@"<(/?)([A-Za-z0-9._]+)>", RegexOptions.Compiled)]
    private static partial Regex TagPattern();
}
