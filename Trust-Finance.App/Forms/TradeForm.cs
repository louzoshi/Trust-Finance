using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Forms;

/// <summary>
/// What the trade screen collects. Deliberately carries no validation attributes: the
/// rules live on <see cref="Trade"/>, and duplicating them here would create a second
/// copy to forget to update. Everything is nullable so a blank field arrives as blank
/// rather than as a zero the entity would have to guess about.
/// </summary>
public class TradeForm
{
    public string Ticker { get; set; } = string.Empty;
    public AssetClass Class { get; set; } = AssetClass.Stock;
    public TradeSide Side { get; set; } = TradeSide.Buy;
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal Fees { get; set; }
    public DateOnly? Date { get; set; }
    public string? Note { get; set; }

    public static TradeForm Empty(DateOnly today) => new() { Date = today };

    public static TradeForm From(Trade t) => new()
    {
        Ticker = t.Ticker,
        Class = t.Class,
        Side = t.Side,
        Quantity = t.Quantity,
        UnitPrice = t.UnitPrice,
        Fees = t.Fees,
        Date = t.Date,
        Note = t.Note
    };

    /// <summary>
    /// Builds the entity, which is what decides whether the input was any good. A blank
    /// number becomes zero and a blank date becomes default — both of which the entity
    /// rejects with a message, so nothing has to be checked twice.
    /// </summary>
    public Trade ToTrade(int userId) => new(
        Ticker,
        Class,
        Side,
        Quantity ?? 0m,
        UnitPrice ?? 0m,
        Fees,
        Date ?? default,
        userId,
        Note);

    /// <summary>What the operation costs or returns, shown live under the form.</summary>
    public decimal Total => Side == TradeSide.Buy
        ? (Quantity ?? 0) * (UnitPrice ?? 0) + Fees
        : (Quantity ?? 0) * (UnitPrice ?? 0) - Fees;
}
