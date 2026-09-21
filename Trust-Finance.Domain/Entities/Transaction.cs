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
        int accountId,
        int userId)
    {
        Describe(description);

        AddNotificationIf(amount <= 0, nameof(Amount),
            "O valor precisa ser maior que zero — a direção é definida pelo tipo, não pelo sinal");
        AddNotificationIf(date == default, nameof(Date), "Informe a data");
        AddNotificationIf(!Enum.IsDefined(type), nameof(Type), "Tipo inválido");
        AddNotificationIf(categoryId <= 0, nameof(CategoryId), "Escolha uma categoria");
        AddNotificationIf(accountId <= 0, nameof(AccountId), "Escolha uma conta");
        AddNotificationIf(userId <= 0, nameof(UserId), "Lançamento sem dono");

        Amount = amount;
        Date = date;
        Type = type;
        CategoryId = categoryId;
        AccountId = accountId;
        UserId = userId;
    }

    /// <summary>One occurrence of a recurrence, on the date it fell due.</summary>
    public Transaction(RecurringTransaction source, DateOnly date)
        : this(source.Description, source.Amount, date, source.Type, source.CategoryId, source.AccountId, source.UserId)
    {
        RecurringTransactionId = source.Id;
    }

    /// <summary>
    /// The two halves of a transfer: money leaving <paramref name="from"/> and arriving
    /// in <paramref name="to"/>, tied by one id. Neither half is income or expense in
    /// any summary — moving money between pockets is not earning or spending it. Paying
    /// a card statement is exactly this, from the checking account to the card.
    /// </summary>
    public static (Transaction Out, Transaction In) Transfer(
        string description, decimal amount, DateOnly date, int categoryId, int from, int to, int userId)
    {
        var id = Guid.NewGuid();
        var outgoing = new Transaction(description, amount, date, TransactionType.Expense, categoryId, from, userId) { TransferId = id };
        var incoming = new Transaction(description, amount, date, TransactionType.Income, categoryId, to, userId) { TransferId = id };

        outgoing.AddNotificationIf(from == to, nameof(AccountId), "A transferência precisa de duas contas diferentes");
        return (outgoing, incoming);
    }

    /// <summary>
    /// One purchase split into <paramref name="count"/> monthly instalments. The cents
    /// that do not divide evenly go on the first one, so the sum is exact. Each carries
    /// "(n/count)" in its description and the shared group id.
    /// </summary>
    public static List<Transaction> Installments(
        string description, decimal total, DateOnly firstDate, TransactionType type, int categoryId, int accountId, int userId, int count)
    {
        if (count < 2)
            return [new Transaction(description, total, firstDate, type, categoryId, accountId, userId)];

        var group = Guid.NewGuid();
        var each = Math.Floor(total / count * 100m) / 100m;
        var remainder = total - each * count;

        var list = new List<Transaction>(count);
        for (var n = 1; n <= count; n++)
        {
            var amount = n == 1 ? each + remainder : each;
            var t = new Transaction($"{description} ({n}/{count})", amount, firstDate.AddMonths(n - 1), type, categoryId, accountId, userId)
            {
                InstallmentGroup = group,
                InstallmentNumber = n,
                InstallmentCount = count
            };
            list.Add(t);
        }

        return list;
    }

    public int Id { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateOnly Date { get; private set; }
    public TransactionType Type { get; private set; }

    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public int AccountId { get; private set; }
    public Account Account { get; private set; } = null!;

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    /// <summary>
    /// The bank's own id for this line (the OFX FITID). Importing the same statement
    /// twice finds it and skips the row; that is what makes the import safe to repeat.
    /// </summary>
    public string? ExternalId { get; private set; }

    /// <summary>Set on both halves of a transfer. See <see cref="Transfer"/>.</summary>
    public Guid? TransferId { get; private set; }

    public bool IsTransfer => TransferId is not null;

    public Guid? InstallmentGroup { get; private set; }
    public int? InstallmentNumber { get; private set; }
    public int? InstallmentCount { get; private set; }

    /// <summary>
    /// Set when this row was posted by a recurrence. It is a trace, not a bond: the row
    /// stays editable and deletable on its own, and outlives the recurrence.
    /// </summary>
    public int? RecurringTransactionId { get; private set; }
    public RecurringTransaction? Recurrence { get; private set; }

    public bool IsRecurring => RecurringTransactionId is not null;

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
        AccountId = corrected.AccountId;
    }

    /// <summary>Ties this row to the bank line it turned out to be, so the next import knows it.</summary>
    public void Reconcile(string externalId)
    {
        AddNotificationIf(string.IsNullOrWhiteSpace(externalId), nameof(ExternalId), "Identificador do banco vazio");

        if (IsValid)
            ExternalId = externalId.Trim();
    }

    /// <summary>A row that came from a statement, carrying the bank's id from the start.</summary>
    public static Transaction Imported(
        string description, decimal amount, DateOnly date, TransactionType type, int categoryId, int accountId, int userId, string externalId)
    {
        var t = new Transaction(description, amount, date, type, categoryId, accountId, userId);
        t.Reconcile(externalId);
        return t;
    }
}
