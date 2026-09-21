using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Forms;

/// <summary>What the recurrences screen collects. The rules live on <see cref="RecurringTransaction"/>.</summary>
public class RecurrenceForm
{
    public string Description { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public TransactionType Type { get; set; } = TransactionType.Expense;
    public int CategoryId { get; set; }
    public Frequency Frequency { get; set; } = Frequency.Monthly;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public static RecurrenceForm Empty(DateOnly today) => new() { StartDate = today };

    public static RecurrenceForm From(RecurringTransaction r) => new()
    {
        Description = r.Description,
        Amount = r.Amount,
        Type = r.Type,
        CategoryId = r.CategoryId,
        Frequency = r.Frequency,
        StartDate = r.StartDate,
        EndDate = r.EndDate
    };

    public RecurringTransaction ToRecurrence(int userId) => new(
        Description,
        Amount ?? 0m,
        Type,
        CategoryId,
        Frequency,
        StartDate ?? default,
        EndDate,
        userId);
}
