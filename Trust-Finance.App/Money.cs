using System.Globalization;
using TrustFinance.Domain.Entities;

namespace TrustFinance.App;

public static class Money
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>R$ 1.234,56</summary>
    public static string Format(decimal value) => value.ToString("C2", Culture);

    /// <summary>
    /// +R$ 1.234,56 / −R$ 1.234,56. The sign is spelled out rather than left to colour
    /// alone, so the direction survives colour blindness and greyscale printing.
    /// </summary>
    public static string Signed(decimal value)
        => (value < 0 ? "−" : "+") + Format(Math.Abs(value));

    public static string Signed(decimal amount, TransactionType type)
        => Signed(type == TransactionType.Income ? amount : -amount);

    /// <summary>R$ 1,2 mi — for axis labels and anywhere the cents are noise.</summary>
    public static string Compact(decimal value)
    {
        var abs = Math.Abs(value);
        return abs switch
        {
            >= 1_000_000_000 => $"R$ {value / 1_000_000_000:N1} bi",
            >= 1_000_000 => $"R$ {value / 1_000_000:N1} mi",
            >= 1_000 => $"R$ {value / 1_000:N1} mil",
            _ => Format(value)
        };
    }

    /// <summary>+12,34% — null becomes an em dash rather than a misleading zero.</summary>
    public static string Percent(decimal? value, bool signed = true)
    {
        if (value is null) return "—";

        var text = Math.Abs(value.Value).ToString("N2", Culture) + "%";
        if (!signed) return value.Value.ToString("N2", Culture) + "%";

        return (value.Value < 0 ? "−" : "+") + text;
    }

    /// <summary>
    /// Quantities shown the way the asset trades: whole shares for equities, fractions
    /// for crypto, with trailing zeros trimmed so 0,50000000 BTC reads as 0,5.
    /// </summary>
    public static string Quantity(decimal value, AssetClass assetClass)
    {
        if (assetClass != AssetClass.Crypto)
            return value.ToString("N0", Culture);

        return value.ToString("0.########", Culture);
    }

    /// <summary>The CSS class that colours a number by its direction.</summary>
    public static string Tone(decimal? value) => value switch
    {
        > 0 => "text-income",
        < 0 => "text-expense",
        _ => "text-muted"
    };
}
