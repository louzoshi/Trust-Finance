using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Services;

/// <summary>Reads the audit trail. Nothing writes it from here — the database context does that on every save.</summary>
public class AuditService(IDbContextFactory<TrustFinanceDbContext> factory)
{
    public const int DefaultLimit = 200;

    /// <summary>The newest entries for one user, optionally narrowed to a single kind of record.</summary>
    public async Task<List<AuditEntry>> RecentAsync(int userId, string? entity = null, int limit = DefaultLimit)
    {
        await using var db = await factory.CreateDbContextAsync();

        var query = db.AuditEntries
            .AsNoTracking()
            .Where(a => a.UserId == userId);

        if (!string.IsNullOrEmpty(entity))
            query = query.Where(a => a.Entity == entity);

        return await query
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>The kinds of record this user has actually touched, for the filter.</summary>
    public async Task<List<string>> EntitiesAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        return await db.AuditEntries
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.Entity)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();
    }
}
