using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Planning;

namespace TrustFinance.App.Services;

/// <summary>
/// Everything the bell shows. Nothing here is stored as a notification: the tray is
/// derived from live state each time it is asked for, so it can never claim a budget is
/// blown after the transaction was deleted. Only "this alert fired" and "the user
/// dismissed it" are persisted, because those are facts nothing else records.
/// </summary>
public class NotificationService(
    IDbContextFactory<TrustFinanceDbContext> factory,
    SettingsService settings,
    AlertService alerts,
    BudgetService budgets,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<Notice>> GetAsync(int userId)
    {
        var user = await settings.GetAsync(userId);
        var now = clock.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);

        // Sweep before reading, so opening the tray is what makes a crossed target news.
        if (user.NotifyPriceAlerts)
            await alerts.EvaluateAsync(userId);

        var triggered = await alerts.GetAsync(userId);
        var budgetStatus = user.NotifyBudgetOverruns ? await budgets.GetStatusAsync(userId) : [];

        var contribution = new GoalProgress(
            "Aporte do mês",
            await ContributedThisMonthAsync(userId, today),
            user.MonthlyContributionGoal);

        return Notices.Build(triggered, budgetStatus, contribution, user, now);
    }

    public async Task<int> CountAsync(int userId) => (await GetAsync(userId)).Count;

    public Task DismissAllAsync(int userId) => alerts.DismissAllAsync(userId);

    private async Task<decimal> ContributedThisMonthAsync(int userId, DateOnly today)
    {
        var firstDay = new DateOnly(today.Year, today.Month, 1);

        await using var db = await factory.CreateDbContextAsync();
        var trades = await db.Trades
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= firstDay)
            .ToListAsync();

        return Planning.ContributedThisMonth(trades, today);
    }
}
