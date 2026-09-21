using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;
using TrustFinance.Domain.Finance;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.App.Services;

public class AccountService(IDbContextFactory<TrustFinanceDbContext> factory, TimeProvider clock)
{
    public const string DefaultName = "Conta corrente";

    /// <summary>
    /// The user's accounts, cash first. A user with none gets a checking account on the
    /// spot: every transaction needs one, and asking a new user to set up "accounts"
    /// before recording their first expense is a wall between them and the product.
    /// </summary>
    public async Task<List<Account>> GetAllAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var accounts = await Query(db, userId).ToListAsync();
        if (accounts.Count > 0)
            return accounts;

        var checking = new Account(DefaultName, AccountKind.Checking, userId);
        db.Accounts.Add(checking);
        await db.SaveChangesAsync();
        return [checking];
    }

    public async Task<Account?> GetByIdAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    }

    public async Task<IReadOnlyList<AccountBalance>> BalancesAsync(int userId)
    {
        var accounts = await GetAllAsync(userId);

        await using var db = await factory.CreateDbContextAsync();
        var transactions = await db.Transactions.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();

        return CardStatements.Balances(accounts, transactions);
    }

    public async Task<IReadOnlyList<CardStatement>> StatementsAsync(int cardId, int userId)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

        await using var db = await factory.CreateDbContextAsync();

        var card = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == cardId && a.UserId == userId);
        if (card is null || !card.IsCreditCard)
            return [];

        var lines = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.AccountId == cardId)
            .ToListAsync();

        return CardStatements.Build(card, lines, today);
    }

    public async Task<Result<Account>> CreateAsync(Account account)
    {
        if (account.IsInvalid)
            return Result<Account>.Fail(account);

        await using var db = await factory.CreateDbContextAsync();

        if (await db.Accounts.AnyAsync(a => a.UserId == account.UserId && a.Name == account.Name))
            return Result<Account>.Fail(nameof(Account.Name), "Já existe uma conta com esse nome");

        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return Result<Account>.Ok(account);
    }

    public async Task<Result> UpdateAsync(int id, Account corrected)
    {
        if (corrected.IsInvalid)
            return Result.Fail(corrected);

        await using var db = await factory.CreateDbContextAsync();

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == corrected.UserId);
        if (account is null)
            return Result.Fail(nameof(Account), "Conta não encontrada");

        if (await db.Accounts.AnyAsync(a => a.UserId == corrected.UserId && a.Name == corrected.Name && a.Id != id))
            return Result.Fail(nameof(Account.Name), "Já existe uma conta com esse nome");

        // Turning a card into a checking account, or back, would silently change what
        // every row on it means; a rename or a new closing day is fine.
        if (account.IsCreditCard != corrected.IsCreditCard && await db.Transactions.AnyAsync(t => t.AccountId == id))
            return Result.Fail(nameof(Account.Kind), "Uma conta com lançamentos não pode mudar entre cartão e conta");

        account.CorrectTo(corrected);
        if (account.IsInvalid)
            return Result.Fail(account);

        await db.SaveChangesAsync();
        return Result.Ok();
    }

    /// <summary>Refused while any row points at it; history is never orphaned.</summary>
    public async Task<Result> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (account is null)
            return Result.Fail(nameof(Account), "Conta não encontrada");

        if (await db.Transactions.AnyAsync(t => t.AccountId == id) || await db.RecurringTransactions.AnyAsync(r => r.AccountId == id))
            return Result.Fail(nameof(Account), "A conta tem lançamentos — mova ou exclua os lançamentos antes");

        if (await db.Accounts.CountAsync(a => a.UserId == userId) == 1)
            return Result.Fail(nameof(Account), "Você precisa de pelo menos uma conta");

        db.Accounts.Remove(account);
        await db.SaveChangesAsync();
        return Result.Ok();
    }

    private static IQueryable<Account> Query(TrustFinanceDbContext db, int userId)
        => db.Accounts.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Kind == AccountKind.CreditCard)
            .ThenBy(a => a.Id);
}
