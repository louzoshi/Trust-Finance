using TrustFinance.Domain.Notifications;

namespace TrustFinance.Domain.Entities;

public enum AccountKind
{
    Checking = 1,
    Savings = 2,
    Cash = 3,

    /// <summary>A liability: purchases pile up until the statement is paid from another account.</summary>
    CreditCard = 4
}

public static class AccountKinds
{
    /// <summary>Whether a balance here is money on hand, as opposed to money owed.</summary>
    public static bool IsCash(this AccountKind kind) => kind != AccountKind.CreditCard;
}

/// <summary>
/// Where money sits, or in the case of a card, where it is owed. Every transaction
/// belongs to one. A card carries the two days that define its statement cycle: the
/// day the statement closes and the day it is due.
/// </summary>
public class Account : Notifiable, IAuditable
{
    public const int MinNameLength = 2;
    public const int MaxNameLength = 40;

    private Account() { }

    public Account(string name, AccountKind kind, int userId, int? closingDay = null, int? dueDay = null)
    {
        var trimmed = (name ?? string.Empty).Trim();

        AddNotificationIf(trimmed.Length is < MinNameLength or > MaxNameLength, nameof(Name),
            $"O nome deve ter entre {MinNameLength} e {MaxNameLength} caracteres");
        AddNotificationIf(!Enum.IsDefined(kind), nameof(Kind), "Tipo de conta inválido");
        AddNotificationIf(userId <= 0, nameof(UserId), "Conta sem dono");

        if (kind == AccountKind.CreditCard)
        {
            AddNotificationIf(closingDay is null or < 1 or > 28, nameof(ClosingDay), "Informe o dia de fechamento da fatura (1 a 28)");
            AddNotificationIf(dueDay is null or < 1 or > 28, nameof(DueDay), "Informe o dia de vencimento da fatura (1 a 28)");
        }

        Name = trimmed;
        Kind = kind;
        UserId = userId;
        ClosingDay = kind == AccountKind.CreditCard ? closingDay : null;
        DueDay = kind == AccountKind.CreditCard ? dueDay : null;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AccountKind Kind { get; private set; }

    /// <summary>Card only. Capped at 28 so every month has the day.</summary>
    public int? ClosingDay { get; private set; }
    public int? DueDay { get; private set; }

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public ICollection<Transaction> Transactions { get; private set; } = new List<Transaction>();

    public bool IsCreditCard => Kind == AccountKind.CreditCard;

    /// <summary>
    /// The statement a purchase on <paramref name="date"/> lands on, identified by its
    /// closing date. Purchases after the closing day go to next month's statement — the
    /// rule that makes a card bought with on the day after closing "free" for a month.
    /// </summary>
    public DateOnly StatementClosingFor(DateOnly date)
    {
        if (!IsCreditCard)
            throw new InvalidOperationException("Only a credit card has statements.");

        var closing = new DateOnly(date.Year, date.Month, ClosingDay!.Value);
        return date <= closing ? closing : closing.AddMonths(1);
    }

    /// <summary>When the statement closing on <paramref name="closing"/> has to be paid.</summary>
    public DateOnly DueDateFor(DateOnly closing)
    {
        var due = new DateOnly(closing.Year, closing.Month, DueDay!.Value);
        return due > closing ? due : due.AddMonths(1);
    }

    public void CorrectTo(Account corrected)
    {
        ClearNotifications();
        AddNotifications(corrected);

        if (IsInvalid)
            return;

        Name = corrected.Name;
        Kind = corrected.Kind;
        ClosingDay = corrected.ClosingDay;
        DueDay = corrected.DueDay;
    }
}
