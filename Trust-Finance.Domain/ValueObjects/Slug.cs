using System.Globalization;
using System.Text;

namespace TrustFinance.Domain.ValueObjects;

public static class Slug
{
    /// <summary>
    /// "Mercado & Padaria" → "mercado-padaria". Accents are stripped rather than
    /// escaped so that "Educação" and "Educacao" collide, which is what a person
    /// typing the same category twice expects.
    /// </summary>
    public static string From(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingDash = false;

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsAsciiLetterOrDigit(c))
            {
                if (pendingDash && builder.Length > 0)
                    builder.Append('-');
                pendingDash = false;
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                pendingDash = true;
            }
        }

        return builder.ToString();
    }
}
