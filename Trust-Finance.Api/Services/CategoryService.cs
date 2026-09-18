using Microsoft.EntityFrameworkCore;
using TF.Data;
using TF.Models;

namespace Trust_Finance.Services;

/// <summary>
/// Every method is scoped to the calling user. A category belongs to whoever created it,
/// so a caller can only ever see and change their own — an id belonging to somebody else
/// is indistinguishable from an id that does not exist.
/// </summary>
public class CategoryService
{
    private readonly TFDataContext _context;

    public CategoryService(TFDataContext context)
    {
        _context = context;
    }

    public async Task<Category> CreateAsync(string name, string slug, int userId)
    {
        if (await SlugIsTakenAsync(slug, userId))
            throw new InvalidOperationException("Slug already exists");

        var category = new Category
        {
            Name = name,
            Slug = slug,
            UserId = userId
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return category;
    }

    public async Task<List<Category>> GetAllAsync(int userId)
        => await _context
            .Categories
            .Where(c => c.UserId == userId)
            .ToListAsync();

    public async Task<Category?> GetByIdAsync(int id, int userId)
        => await _context
            .Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

    public async Task<Category> UpdateAsync(int id, string name, string slug, int userId)
    {
        var category = await GetByIdAsync(id, userId)
            ?? throw new KeyNotFoundException("Category not found");

        // Slug is backed by a unique index; without this check the update surfaces as an
        // unhandled DbUpdateException instead of a 400.
        if (await SlugIsTakenAsync(slug, userId, exceptId: id))
            throw new InvalidOperationException("Slug already exists");

        category.Name = name;
        category.Slug = slug;

        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<Category> DeleteAsync(int id, int userId)
    {
        var category = await GetByIdAsync(id, userId)
            ?? throw new KeyNotFoundException("Category not found");

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return category;
    }

    private async Task<bool> SlugIsTakenAsync(string slug, int userId, int? exceptId = null)
        => await _context
            .Categories
            .AsNoTracking()
            .AnyAsync(c => c.Slug == slug
                        && c.UserId == userId
                        && (exceptId == null || c.Id != exceptId));
}
