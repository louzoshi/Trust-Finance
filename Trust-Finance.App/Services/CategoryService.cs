using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.App.Services;

/// <summary>
/// Every method is scoped to the calling user. A category belongs to whoever created it,
/// so a caller can only ever see and change their own — an id belonging to somebody else
/// is indistinguishable from an id that does not exist.
/// </summary>
public class CategoryService(IDbContextFactory<TrustFinanceDbContext> factory)
{
    public async Task<List<Category>> GetAllAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
    }

    public async Task<Result<Category>> CreateAsync(string name, int userId)
    {
        var category = new Category(name, userId);
        if (category.IsInvalid)
            return Result<Category>.Fail(category);

        await using var db = await factory.CreateDbContextAsync();

        if (await SlugIsTakenAsync(db, category.Slug, userId))
            return Result<Category>.Fail(nameof(Category.Name), "Já existe uma categoria com esse nome");

        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return Result<Category>.Ok(category);
    }

    public async Task<Result> UpdateAsync(int id, string name, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (category is null)
            return Result.Fail(nameof(Category), "Categoria não encontrada");

        category.Rename(name);
        if (category.IsInvalid)
            return Result.Fail(category);

        // Slug is backed by a unique index; without this check the update surfaces as an
        // unhandled DbUpdateException instead of a message the form can show.
        if (await SlugIsTakenAsync(db, category.Slug, userId, exceptId: id))
            return Result.Fail(nameof(Category.Name), "Já existe uma categoria com esse nome");

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        if (category is null)
            return Result.Fail(nameof(Category), "Categoria não encontrada");

        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        return Result.Ok();
    }

    private static Task<bool> SlugIsTakenAsync(TrustFinanceDbContext db, string slug, int userId, int? exceptId = null)
        => db.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Slug == slug
                        && c.UserId == userId
                        && (exceptId == null || c.Id != exceptId));
}
