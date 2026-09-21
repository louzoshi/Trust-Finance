using TrustFinance.Domain.Notifications;

namespace TrustFinance.Domain.Entities;

/// <summary>
/// One movement of cash. The amount is always positive and the direction lives in
/// <see cref="Type"/>, so a sign error cannot quietly turn an expense into income.
/// </summary>
public class Transaction : Notifiable
{
    public const int MinDescriptionLength = 2;
    public const int MaxDescriptionLength = 100;

    private Transaction() { }

    public Transaction(
        string description,
        decimal amount,
        DateOnly date,
        TransactionType type,
        int categoryId,
        int userId)
    {
        Describe(description);

        AddNotificationIf(amount <= 0, nameof(Amount),
            "O valor precisa ser maior que zero — a direção é definida pelo tipo, não pelo sinal");
        AddNotificationIf(date == default, nameof(Date), "Informe a data");
        AddNotificationIf(!Enum.IsDefined(type), nameof(Type), "Tipo inválido");
        AddNotificationIf(categoryId <= 0, nameof(CategoryId), "Escolha uma categoria");
        AddNotificationIf(userId <= 0, nameof(UserId), "Lançamento sem dono");

        Amount = amount;
        Date = date;
        Type = type;
        CategoryId = categoryId;
        UserId = userId;
    }

    public int Id { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateOnly Date { get; private set; }
    public TransactionType Type { get; private set; }

    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>The amount as it affects the balance: positive for income, negative for expense.</summary>
    public decimal SignedAmount => Type == TransactionType.Income ? Amount : -Amount;

    public void Describe(string description)
    {
        var trimmed = (description ?? string.Empty).Trim();

        AddNotificationIf(trimmed.Length is < MinDescriptionLength or > MaxDescriptionLength,
            nameof(Description),
            $"A descrição deve ter entre {MinDescriptionLength} e {MaxDescriptionLength} caracteres");

        if (IsValid)
            Description = trimmed;
    }

    public void CorrectTo(Transaction corrected)
    {
        ClearNotifications();
        AddNotifications(corrected);

        if (IsInvalid)
            return;

        Description = corrected.Description;
        Amount = corrected.Amount;
        Date = corrected.Date;
        Type = corrected.Type;
        CategoryId = corrected.CategoryId;
    }
}
