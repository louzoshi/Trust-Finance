using Microsoft.EntityFrameworkCore;
using TF.Data;
using TF.Models;

namespace Trust_Finance.Services;

public class CategoryService
{
    private readonly TFDataContext _context;

    public CategoryService(TFDataContext context)
    {
        _context = context;
    }

    public async Task<Category> CreateAsync(string name, string slug)
    {
        var exists = await _context
            .Categories
            .AsNoTracking()
            .AnyAsync(x => x.Slug == slug);

        if (exists)
            throw new InvalidOperationException("Slug already exists");

        var category = new Category
        {
            Name = name,
            Slug = slug
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return category;
    }

    public async Task<List<Category>> GetAllAsync()
        => await _context.Categories.ToListAsync();

    public async Task<Category?> GetByIdAsync(int id)
        => await _context.Categories.FirstOrDefaultAsync(x => x.Id == id);

    public async Task<Category> UpdateAsync(int id, string name, string slug)
    {
        var category = await GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Category not found");

        // Slug is backed by a unique index; without this check the update surfaces as an
        // unhandled DbUpdateException instead of a 400.
        var slugTaken = await _context
            .Categories
            .AsNoTracking()
            .AnyAsync(x => x.Slug == slug && x.Id != id);

        if (slugTaken)
            throw new InvalidOperationException("Slug already exists");

        category.Name = name;
        category.Slug = slug;

        await _context.SaveChangesAsync();
        return category;
    }

    public async Task<Category> DeleteAsync(int id)
    {
        var category = await GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Category not found");

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return category;
    }
}
