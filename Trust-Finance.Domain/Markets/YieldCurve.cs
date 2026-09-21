namespace TrustFinance.Domain.Markets;

/// <summary>One point of the term structure: a rate for a term measured in business days.</summary>
public readonly record struct CurvePoint(int BusinessDays, decimal AnnualPercent);

/// <summary>
/// The term structure of interest rates, interpolated the way the Brazilian market
/// interpolates it: <b>flat forward</b>.
///
/// Between two quoted vertices the forward rate is held constant, which is the same as
/// interpolating linearly on the logarithm of the accumulated factor. Interpolating the
/// rates themselves — the obvious thing — produces forward rates that jump at every
/// vertex and prices that disagree with the market by more than the bid-ask.
///
/// Outside the quoted range the curve is flat at the nearest vertex. Extrapolating a
/// trend past the last liquid vertex invents a number the market never made.
/// </summary>
public sealed class YieldCurve
{
    private readonly CurvePoint[] _points;

    public YieldCurve(IEnumerable<CurvePoint> points, DateOnly? reference = null)
    {
        _points = [.. points.Where(p => p.BusinessDays > 0).OrderBy(p => p.BusinessDays)];

        if (_points.Length == 0)
            throw new ArgumentException("Uma curva precisa de ao menos um vértice.", nameof(points));

        Reference = reference;
    }

    /// <summary>The day the curve was observed, when it came from a source that says so.</summary>
    public DateOnly? Reference { get; }

    public IReadOnlyList<CurvePoint> Points => _points;

    /// <summary>A single rate for every term — what a CDB quoted at one number implies.</summary>
    public static YieldCurve Flat(decimal annualPercent, DateOnly? reference = null)
        => new([new CurvePoint(BusinessCalendar.YearInBusinessDays, annualPercent)], reference);

    /// <summary>The annual rate for <paramref name="businessDays"/>, interpolated flat forward.</summary>
    public decimal RateFor(int businessDays)
    {
        if (businessDays <= 0)
            return _points[0].AnnualPercent;

        if (businessDays <= _points[0].BusinessDays)
            return _points[0].AnnualPercent;

        var last = _points[^1];
        if (businessDays >= last.BusinessDays)
            return last.AnnualPercent;

        var i = 0;
        while (_points[i + 1].BusinessDays < businessDays)
            i++;

        var left = _points[i];
        var right = _points[i + 1];

        // Flat forward: the accumulated factor grows at a constant forward rate between
        // the two vertices, so the factor at t is the left factor times the forward
        // factor over the elapsed slice of the gap.
        var leftFactor = Math.Pow(1 + (double)left.AnnualPercent / 100, left.BusinessDays / 252.0);
        var rightFactor = Math.Pow(1 + (double)right.AnnualPercent / 100, right.BusinessDays / 252.0);

        var slice = (double)(businessDays - left.BusinessDays) / (right.BusinessDays - left.BusinessDays);
        var factor = leftFactor * Math.Pow(rightFactor / leftFactor, slice);

        return (decimal)(Math.Pow(factor, 252.0 / businessDays) - 1) * 100m;
    }

    /// <summary>The discount factor for <paramref name="businessDays"/>: what R$ 1 then is worth now.</summary>
    public decimal DiscountFactor(int businessDays)
        => businessDays <= 0 ? 1m : 1m / Rates.Factor(RateFor(businessDays), businessDays);
}
