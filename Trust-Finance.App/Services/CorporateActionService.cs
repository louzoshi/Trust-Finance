using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

public class CorporateActionService(IDbContextFactory<TrustFinanceDbContext> factory)
{
    public async Task<List<CorporateAction>> GetAllAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CorporateActions
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.Id)
            .ToListAsync();
    }

    public async Task<Result<CorporateAction>> CreateAsync(CorporateAction action)
    {
        if (action.IsInvalid)
            return Result<CorporateAction>.Fail(action);

        await using var db = await factory.CreateDbContextAsync();
        db.CorporateActions.Add(action);
        await db.SaveChangesAsync();
        return Result<CorporateAction>.Ok(action);
    }

    public async Task<Result> UpdateAsync(int id, CorporateAction corrected)
    {
        if (corrected.IsInvalid)
            return Result.Fail(corrected);

        await using var db = await factory.CreateDbContextAsync();

        var action = await db.CorporateActions.FirstOrDefaultAsync(a => a.Id == id && a.UserId == corrected.UserId);
        if (action is null)
            return Result.Fail(nameof(CorporateAction), "Evento não encontrado");

        action.CorrectTo(corrected);
        if (action.IsInvalid)
            return Result.Fail(action);

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var action = await db.CorporateActions.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (action is null)
            return Result.Fail(nameof(CorporateAction), "Evento não encontrado");

        db.CorporateActions.Remove(action);
        await db.SaveChangesAsync();
        return Result.Ok();
    }
}
