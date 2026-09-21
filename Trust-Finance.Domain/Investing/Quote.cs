using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Investing;

/// <summary>
/// A price for one ticker at one moment, as returned by a market-data provider. Every
/// field beyond price is optional: providers differ in what they cover, and a missing
/// dividend yield has to read as "unknown" rather than zero.
/// </summary>
public sealed record Quote(
    string Ticker,
    string Name,
    AssetClass Class,
    decimal Price,
    decimal? PreviousClose = null,
    decimal? DividendYield = null,
    decimal? MarketCap = null,
    DateTime? UpdatedAt = null)
{
    /// <summary>Change since the previous close, in currency. Null when the provider gave no close to compare against.</summary>
    public decimal? Change => PreviousClose is > 0 ? Price - PreviousClose : null;

    public decimal? ChangePercent =>
        PreviousClose is > 0 ? (Price - PreviousClose.Value) / PreviousClose.Value * 100m : null;

    public bool IsUp => Change is > 0;
    public bool IsDown => Change is < 0;
}

/// <summary>
/// The rates every Brazilian portfolio is measured against. CDI is the benchmark a
/// return is called good or bad relative to; IPCA is what it has to beat to be real.
/// </summary>
public sealed record MarketIndicators(
    decimal CdiAnnual,
    decimal SelicAnnual,
    decimal IpcaAnnual,
    DateTime UpdatedAt)
{
    /// <summary>What <paramref name="amount"/> would have become at CDI over <paramref name="days"/> calendar days.</summary>
    public decimal CdiProjection(decimal amount, int days)
    {
        if (amount <= 0 || days <= 0) return amount;
        var years = days / 365m;
        var growth = Math.Pow(1 + (double)(CdiAnnual / 100m), (double)years);
        return Math.Round(amount * (decimal)growth, 2);
    }
}
