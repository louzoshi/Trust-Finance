using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

public class CategoryRuleService(IDbContextFactory<TrustFinanceDbContext> factory)
{
    public async Task<List<CategoryRule>> GetAllAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CategoryRules
            .AsNoTracking()
            .Include(r => r.Category)
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Category.Name).ThenBy(r => r.Pattern)
            .ToListAsync();
    }

    /// <summary>The category the rules pick for a statement line, or null when none match. Longest pattern wins.</summary>
    public static int? Suggest(IEnumerable<CategoryRule> rules, string memo)
        => rules.Where(r => r.Matches(memo)).OrderByDescending(r => r.Pattern.Length).FirstOrDefault()?.CategoryId;

    public async Task<Result> CreateAsync(CategoryRule rule)
    {
        if (rule.IsInvalid)
            return Result.Fail(rule);

        await using var db = await factory.CreateDbContextAsync();

        if (!await db.Categories.AnyAsync(c => c.Id == rule.CategoryId && c.UserId == rule.UserId))
            return Result.Fail(nameof(CategoryRule.CategoryId), "Categoria não encontrada");

        var existing = await db.CategoryRules.FirstOrDefaultAsync(r => r.UserId == rule.UserId && r.Pattern == rule.Pattern);
        if (existing is not null)
        {
            db.CategoryRules.Remove(existing);
        }

        db.CategoryRules.Add(rule);
        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var rule = await db.CategoryRules.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
        if (rule is null)
            return Result.Fail(nameof(CategoryRule), "Regra não encontrada");

        db.CategoryRules.Remove(rule);
        await db.SaveChangesAsync();
        return Result.Ok();
    }
}
