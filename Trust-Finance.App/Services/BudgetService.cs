using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.Planning;

namespace TrustFinance.App.Services;

public class BudgetService(IDbContextFactory<TrustFinanceDbContext> factory, TimeProvider clock)
{
    public async Task<List<Budget>> GetAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.Category.Name)
            .ToListAsync();
    }

    /// <summary>How each budgeted category is tracking in the current month.</summary>
    public async Task<IReadOnlyList<BudgetStatus>> GetStatusAsync(int userId)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        await using var db = await factory.CreateDbContextAsync();

        var budgets = await db.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Where(b => b.UserId == userId)
            .ToListAsync();

        if (budgets.Count == 0)
            return [];

        // Only this month's rows are needed; the ledger can be years long.
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        var transactions = await db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= firstDay)
            .ToListAsync();

        var names = budgets.ToDictionary(b => b.CategoryId, b => b.Category.Name);
        return Planning.Budgets(budgets, names, transactions, today);
    }

    /// <summary>Sets a category's ceiling, or clears it when the limit is zero or less.</summary>
    public async Task<Result> SetAsync(int categoryId, decimal monthlyLimit, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        if (!await db.Categories.AnyAsync(c => c.Id == categoryId && c.UserId == userId))
            return Result.Fail(nameof(Budget.CategoryId), "Categoria não encontrada");

        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.CategoryId == categoryId && b.UserId == userId);

        if (monthlyLimit <= 0)
        {
            if (budget is not null)
            {
                db.Budgets.Remove(budget);
                await db.SaveChangesAsync();
            }
            return Result.Ok();
        }

        if (budget is null)
        {
            budget = new Budget(categoryId, monthlyLimit, userId);
            if (budget.IsInvalid)
                return Result.Fail(budget);

            db.Budgets.Add(budget);
        }
        else
        {
            budget.ChangeLimitTo(monthlyLimit);
            if (budget.IsInvalid)
                return Result.Fail(budget);
        }

        await db.SaveChangesAsync();
        return Result.Ok();
    }
}
