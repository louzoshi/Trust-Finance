using TrustFinance.Domain.Entities;

namespace TrustFinance.Domain.Investing;

/// <summary>Running average price, quantity and banked result for one ticker.</summary>
internal sealed class Book(string ticker, AssetClass assetClass)
{
    public string Ticker { get; } = ticker;
    public AssetClass Class { get; private set; } = assetClass;
    public decimal Quantity { get; private set; }
    public decimal AveragePrice { get; private set; }
    public decimal RealizedPnL { get; private set; }

    public bool IsOpen => Quantity > 0;

    public void Apply(Trade t)
    {
        // A later trade is the better witness to what the asset is: a ticker first
        // entered under the wrong class gets corrected by the next one.
        Class = t.Class;

        if (t.Side == TradeSide.Buy)
        {
            var cost = t.Gross + t.Fees;
            var total = Quantity + t.Quantity;
            AveragePrice = total > 0 ? (Quantity * AveragePrice + cost) / total : 0m;
            Quantity = total;
            return;
        }

        // Selling more than is held would produce a negative position and a nonsense
        // average. Cap it: the ledger is wrong, but the summary stays readable.
        var sold = Math.Min(t.Quantity, Quantity);
        if (sold <= 0)
            return;

        var proceeds = sold * t.UnitPrice - t.Fees;
        RealizedPnL += proceeds - sold * AveragePrice;
        Quantity -= sold;

        if (Quantity == 0)
            AveragePrice = 0m;
    }

    /// <summary>
    /// Splits and reverse splits keep the cost basis and change how many pieces it is
    /// spread over. A bonus adds shares at the cost the issuer declared, which lifts the
    /// basis and moves the average — the tax authority treats those shares as bought at
    /// that price. Fractions that a reverse split or bonus would leave are dropped: on
    /// the exchange they are sold off as leftovers, they never sit in the position.
    /// </summary>
    public void Apply(CorporateAction a)
    {
        if (!IsOpen)
            return;

        var decimals = Class.QuantityDecimals();

        switch (a.Kind)
        {
            case CorporateActionKind.Split:
            case CorporateActionKind.ReverseSplit:
                Quantity = Floor(Quantity * a.Factor, decimals);
                AveragePrice /= a.Factor;
                break;

            case CorporateActionKind.Bonus:
                var granted = Floor(Quantity * a.Factor, decimals);
                if (granted <= 0)
                    return;

                var cost = granted * (a.BonusUnitCost ?? 0m);
                var total = Quantity + granted;
                AveragePrice = (Quantity * AveragePrice + cost) / total;
                Quantity = total;
                break;
        }

        if (Quantity == 0)
            AveragePrice = 0m;
    }

    private static decimal Floor(decimal value, int decimals)
    {
        var scale = (decimal)Math.Pow(10, decimals);
        return Math.Floor(value * scale) / scale;
    }
}
