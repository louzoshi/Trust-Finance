using TrustFinance.Domain.Finance;
using TrustFinance.Domain.Notifications;

namespace TrustFinance.Domain.Entities;

/// <summary>
/// A transaction that repeats: salary, rent, a subscription. It is a template, not a
/// movement of cash — nothing here counts towards a balance until <see cref="PostDue"/>
/// turns a due occurrence into a real <see cref="Transaction"/>.
///
/// Posting real rows rather than computing occurrences on the fly is deliberate. A
/// posted row can be edited when the bill came in a little higher, deleted when it was
/// skipped, and it keeps standing when the recurrence is later removed. The recurrence
/// only ever decides the future.
/// </summary>
public class RecurringTransaction : Notifiable
{
    private RecurringTransaction() { }

    public RecurringTransaction(
        string description,
        decimal amount,
        TransactionType type,
        int categoryId,
        int accountId,
        Frequency frequency,
        DateOnly startDate,
        DateOnly? endDate,
        int userId)
    {
        var trimmed = (description ?? string.Empty).Trim();

        AddNotificationIf(trimmed.Length is < Transaction.MinDescriptionLength or > Transaction.MaxDescriptionLength,
            nameof(Description),
            $"A descrição deve ter entre {Transaction.MinDescriptionLength} e {Transaction.MaxDescriptionLength} caracteres");
        AddNotificationIf(amount <= 0, nameof(Amount),
            "O valor precisa ser maior que zero — a direção é definida pelo tipo, não pelo sinal");
        AddNotificationIf(!Enum.IsDefined(type), nameof(Type), "Tipo inválido");
        AddNotificationIf(categoryId <= 0, nameof(CategoryId), "Escolha uma categoria");
        AddNotificationIf(accountId <= 0, nameof(AccountId), "Escolha uma conta");
        AddNotificationIf(!Enum.IsDefined(frequency), nameof(Frequency), "Escolha a frequência");
        AddNotificationIf(startDate == default, nameof(StartDate), "Informe a primeira data");
        AddNotificationIf(endDate is not null && endDate < startDate, nameof(EndDate),
            "A data final precisa ser igual ou posterior à primeira");
        AddNotificationIf(userId <= 0, nameof(UserId), "Recorrência sem dono");

        Description = trimmed;
        Amount = amount;
        Type = type;
        CategoryId = categoryId;
        AccountId = accountId;
        Frequency = frequency;
        StartDate = startDate;
        EndDate = endDate;
        NextDate = startDate;
        UserId = userId;
    }

    public int Id { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public TransactionType Type { get; private set; }
    public Frequency Frequency { get; private set; }

    /// <summary>The anchor every occurrence is computed from; also the first one.</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Last date an occurrence may fall on. Null means until deleted.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>The next occurrence still to be posted. Only ever moves forward.</summary>
    public DateOnly NextDate { get; private set; }

    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public int AccountId { get; private set; }
    public Account Account { get; private set; } = null!;

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public ICollection<Transaction> Transactions { get; private set; } = new List<Transaction>();

    /// <summary>True once the end date has passed and nothing is left to post.</summary>
    public bool IsFinished => EndDate is not null && NextDate > EndDate;

    /// <summary>
    /// Every occurrence due on or before <paramref name="today"/> as a transaction, and
    /// advances <see cref="NextDate"/> past them. Calling this twice on the same day posts
    /// nothing the second time, which is what lets every page trigger it without care.
    /// </summary>
    public List<Transaction> PostDue(DateOnly today)
    {
        var posted = new List<Transaction>();

        while (NextDate <= today && (EndDate is null || NextDate <= EndDate))
        {
            posted.Add(new Transaction(this, NextDate));
            NextDate = Schedule.NextAfter(StartDate, Frequency, NextDate);
        }

        return posted;
    }

    /// <summary>
    /// Takes the corrected values. What was already posted is never posted again: the next
    /// date is recomputed under the new schedule but is not allowed to move backwards, so
    /// changing a salary's amount does not re-issue last month's, and moving the start
    /// earlier does not back-fill history — that is what the transactions screen is for.
    /// </summary>
    public void CorrectTo(RecurringTransaction corrected)
    {
        ClearNotifications();
        AddNotifications(corrected);

        if (IsInvalid)
            return;

        Description = corrected.Description;
        Amount = corrected.Amount;
        Type = corrected.Type;
        CategoryId = corrected.CategoryId;
        AccountId = corrected.AccountId;
        Frequency = corrected.Frequency;
        StartDate = corrected.StartDate;
        EndDate = corrected.EndDate;

        var floor = NextDate > StartDate ? NextDate : StartDate;
        NextDate = Schedule.NextOnOrAfter(StartDate, Frequency, floor);
    }
}
