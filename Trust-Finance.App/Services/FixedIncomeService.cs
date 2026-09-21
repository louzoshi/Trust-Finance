using Microsoft.EntityFrameworkCore;
using TrustFinance.App.Services.Market;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Markets;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

/// <summary>The whole fixed-income book, valued, with what it means for the FGC.</summary>
public sealed record FixedIncomeBook(
    IReadOnlyList<FixedIncomeValuation> Papers,
    IReadOnlyList<FgcExposure> Fgc,
    bool IndexIsLive)
{
    public IEnumerable<FixedIncomeValuation> Open => Papers.Where(p => p.Investment.IsOpen);

    /// <summary>What the book is worth held to maturity — the number a statement shows.</summary>
    public decimal OnCurve => Open.Sum(p => p.OnCurve);

    /// <summary>What it would fetch if sold today. Below the curve when rates have risen.</summary>
    public decimal MarketValue => Open.Sum(p => p.MarketValue);

    /// <summary>What redeeming everything today would actually leave, after tax and custody.</summary>
    public decimal NetValue => Open.Sum(p => p.NetValue);

    public decimal Invested => Open.Sum(p => p.Investment.Principal);
    public decimal GrossGain => OnCurve - Invested;
    public decimal MarkToMarketGap => MarketValue - OnCurve;

    public bool IsEmpty => !Open.Any();
    public int UnderwaterCount => Open.Count(p => p.IsUnderwater);
    public decimal UncoveredByFgc => Fgc.Sum(e => e.Uncovered);

    /// <summary>Papers maturing within the next month, so the money can be put somewhere.</summary>
    public IEnumerable<FixedIncomeValuation> MaturingSoon(DateOnly today)
        => Open.Where(p => p.Investment.MaturityDate <= today.AddMonths(1));
}

public class FixedIncomeService(
    IDbContextFactory<TrustFinanceDbContext> factory,
    BcbSeries series,
    MarketData market,
    TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public async Task<List<FixedIncomeInvestment>> GetAllAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.FixedIncomeInvestments
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.RedeemedOn != null)
            .ThenBy(f => f.MaturityDate)
            .ToListAsync();
    }

    public async Task<FixedIncomeInvestment?> GetByIdAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.FixedIncomeInvestments.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
    }

    /// <summary>
    /// The book, accrued against the real published indices and marked against a curve
    /// built from today's CDI. A flat curve at the CDI is a poor man's ETTJ, but it is
    /// honest about what it is: it answers "what would this prefixado fetch if the
    /// market charged today's CDI for the remaining term", which is the question that
    /// matters, and it is right for a post-fixed book by construction.
    /// </summary>
    public async Task<FixedIncomeBook> GetBookAsync(int userId)
    {
        var papers = await GetAllAsync(userId);
        if (papers.Count == 0)
            return new FixedIncomeBook([], [], true);

        var from = papers.Min(p => p.PurchaseDate);
        var indicators = await market.IndicatorsAsync(userId);

        var (cdi, cdiIsLive) = await series.GetAsync(BcbSeries.Cdi, from, Today, indicators.CdiAnnual);
        var (selic, _) = await series.GetAsync(BcbSeries.Selic, from, Today, indicators.SelicAnnual);

        // IPCA is monthly and comes as a percentage for the month; it is used as
        // published, which is what makes an IPCA+ paper step rather than drift.
        var (ipca, _) = papers.Any(p => p.Index == IndexKind.IpcaPlus)
            ? await series.GetAsync(BcbSeries.Ipca, from, Today, indicators.IpcaAnnual)
            : ([], true);

        var curve = YieldCurve.Flat(indicators.CdiAnnual, Today);
        var valued = FixedIncomePricing.ValueAll(papers, Today, cdi, selic, ipca, curve);

        return new FixedIncomeBook(valued, Fgc.Exposure(valued), cdiIsLive);
    }

    public async Task<Result<FixedIncomeInvestment>> CreateAsync(FixedIncomeInvestment paper)
    {
        if (paper.IsInvalid)
            return Result<FixedIncomeInvestment>.Fail(paper);

        await using var db = await factory.CreateDbContextAsync();
        db.FixedIncomeInvestments.Add(paper);
        await db.SaveChangesAsync();
        return Result<FixedIncomeInvestment>.Ok(paper);
    }

    public async Task<Result> UpdateAsync(int id, FixedIncomeInvestment corrected)
    {
        if (corrected.IsInvalid)
            return Result.Fail(corrected);

        await using var db = await factory.CreateDbContextAsync();

        var paper = await db.FixedIncomeInvestments.FirstOrDefaultAsync(f => f.Id == id && f.UserId == corrected.UserId);
        if (paper is null)
            return Result.Fail(nameof(FixedIncomeInvestment), "Aplicação não encontrada");

        paper.CorrectTo(corrected);
        if (paper.IsInvalid)
            return Result.Fail(paper);

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    /// <summary>Marks the paper as redeemed on a date; it stops accruing there and leaves the FGC count.</summary>
    public async Task<Result> RedeemAsync(int id, DateOnly date, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var paper = await db.FixedIncomeInvestments.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
        if (paper is null)
            return Result.Fail(nameof(FixedIncomeInvestment), "Aplicação não encontrada");

        paper.Redeem(date);
        if (paper.IsInvalid)
            return Result.Fail(paper);

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var paper = await db.FixedIncomeInvestments.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
        if (paper is null)
            return Result.Fail(nameof(FixedIncomeInvestment), "Aplicação não encontrada");

        db.FixedIncomeInvestments.Remove(paper);
        await db.SaveChangesAsync();
        return Result.Ok();
    }
}
