using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Tests.Fixtures;

/// <summary>
/// A real SQLite database that lives in memory for the length of one test. It runs the
/// actual migrations, so unique indexes, foreign keys and column types all behave as
/// they do in production — the things the EF InMemory provider silently ignores.
/// </summary>
public sealed class SqliteDatabase : IDbContextFactory<TrustFinanceDbContext>, IDisposable
{
    // An in-memory SQLite database exists only while a connection to it is open, so
    // one connection is held for the whole test and shared by every context.
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TrustFinanceDbContext> _options;

    /// <summary>Two users, so every "cannot reach somebody else's record" rule has a somebody else.</summary>
    public int Ada { get; }
    public int Bob { get; }

    public SqliteDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TrustFinanceDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateDbContext();
        db.Database.Migrate();

        // Ids come from the database rather than being assigned: the entities no longer
        // let a caller set one, which is the point of the refactor.
        var ada = new User("Ada", "ada@example.com", "hash");
        var bob = new User("Bob", "bob@example.com", "hash");
        db.Users.AddRange(ada, bob);
        db.SaveChanges();

        Ada = ada.Id;
        Bob = bob.Id;
    }

    public TrustFinanceDbContext CreateDbContext() => new(_options);

    public async Task<Category> AddCategoryAsync(int userId, string name = "Alimentação")
    {
        await using var db = CreateDbContext();
        var category = new Category(name, userId);
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public async Task<Transaction> AddTransactionAsync(
        int userId, int categoryId, decimal amount = 100m,
        TransactionType type = TransactionType.Expense, DateOnly? date = null)
    {
        await using var db = CreateDbContext();
        var transaction = new Transaction(
            "Compra", amount, date ?? new DateOnly(2026, 9, 10), type, categoryId, userId);
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return transaction;
    }

    public void Dispose() => _connection.Dispose();
}
