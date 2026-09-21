using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Forms;

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
    public TransactionType Type { get; set; } = TransactionType.Expense;

    public int CategoryId { get; set; }

    public static TransactionForm Empty(DateOnly today) => new() { Date = today };

    public static TransactionForm From(Transaction t) => new()
    {
        Description = t.Description,
        Amount = t.Amount,
        Date = t.Date,
        Type = t.Type,
        CategoryId = t.CategoryId
    };

    public Transaction ToTransaction(int userId) => new(
        Description,
        Amount ?? 0m,
        Date ?? default,
        Type,
        CategoryId,
        userId);
}
