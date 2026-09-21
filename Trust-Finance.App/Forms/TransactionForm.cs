using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Forms;

/// <summary>What the form is recording: a plain movement, or money changing pockets.</summary>
public enum EntryKind
{
    Expense = 1,
    Income = 2,
    Transfer = 3
}

/// <summary>
/// What the transactions screen collects. The rules live on <see cref="Transaction"/>;
/// see <see cref="TradeForm"/> for why they are not repeated here.
/// </summary>
public class TransactionForm
{
    public string Description { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public DateOnly? Date { get; set; }

    // Most entries in a personal ledger are payments, so this is the cheaper
    // default: it is the one the user has to change least often.
    public EntryKind Kind { get; set; } = EntryKind.Expense;

    public int CategoryId { get; set; }
    public int AccountId { get; set; }

    /// <summary>Transfer only: where the money lands.</summary>
    public int ToAccountId { get; set; }

    /// <summary>1 is a single row; more splits the amount monthly. Cards mostly.</summary>
    public int Installments { get; set; } = 1;

    public TransactionType Type => Kind == EntryKind.Income ? TransactionType.Income : TransactionType.Expense;

    public static TransactionForm Empty(DateOnly today, int accountId) => new() { Date = today, AccountId = accountId };

    public static TransactionForm From(Transaction t) => new()
    {
        Description = t.Description,
        Amount = t.Amount,
        Date = t.Date,
        Kind = t.Type == TransactionType.Income ? EntryKind.Income : EntryKind.Expense,
        CategoryId = t.CategoryId,
        AccountId = t.AccountId
    };

    public Transaction ToTransaction(int userId) => new(
        Description,
        Amount ?? 0m,
        Date ?? default,
        Type,
        CategoryId,
        AccountId,
        userId);

    public List<Transaction> ToInstallments(int userId) => Transaction.Installments(
        Description, Amount ?? 0m, Date ?? default, Type, CategoryId, AccountId, userId, Math.Max(Installments, 1));

    public (Transaction Out, Transaction In) ToTransfer(int userId) => Transaction.Transfer(
        Description, Amount ?? 0m, Date ?? default, CategoryId, AccountId, ToAccountId, userId);
}
