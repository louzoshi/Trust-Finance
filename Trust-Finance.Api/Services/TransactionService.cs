using Microsoft.EntityFrameworkCore;
using TF.Data;
using TF.Models;

namespace Trust_Finance.Services;

public class TransactionService
{
    private readonly TFDataContext _context;

    public TransactionService(TFDataContext context)
    {
        _context = context;
    }

    public async Task<List<Transaction>> GetAllAsync(int userId)
    {
        return await _context
            .Transactions
            .Where(t => t.UserId == userId)
            .ToListAsync();
    }

    public async Task<Transaction?> GetByIdAsync(int id, int userId)
    {
        return await _context
            .Transactions
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
    }

    public async Task<Transaction> CreateAsync(
        string description,
        decimal amount,
        DateTime date,
        TransactionType type,
        int categoryId,
        int userId)
    {
        await EnsureCategoryExistsAsync(categoryId, userId);

        var transaction = new Transaction
        {
            Description = description,
            Amount = amount,
            Date = date,
            Type = type,
            CategoryId = categoryId,
            UserId = userId
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task<Transaction> UpdateAsync(
        int id,
        string description,
        decimal amount,
        DateTime date,
        TransactionType type,
        int categoryId,
        int userId)
    {
        var transaction = await GetByIdAsync(id, userId)
            ?? throw new KeyNotFoundException("Transaction not found");

        await EnsureCategoryExistsAsync(categoryId, userId);

        transaction.Description = description;
        transaction.Amount = amount;
        transaction.Date = date;
        transaction.Type = type;
        transaction.CategoryId = categoryId;

        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task<Transaction> DeleteAsync(int id, int userId)
    {
        var transaction = await GetByIdAsync(id, userId)
            ?? throw new KeyNotFoundException("Transaction not found");

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    /// <summary>
    /// CategoryId is a foreign key: an unknown value fails at SaveChanges with a database
    /// error rather than a readable rejection, so it is validated up front. The check is
    /// scoped to the caller as well — pointing a transaction at somebody else's category
    /// would leak that category's name back through the transaction list.
    /// </summary>
    private async Task EnsureCategoryExistsAsync(int categoryId, int userId)
    {
        var exists = await _context
            .Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == categoryId && c.UserId == userId);

        if (!exists)
            throw new InvalidOperationException("Category not found");
    }
}
