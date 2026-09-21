using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Domain.Entities;

public class User : Notifiable
{
    public const int MinNameLength = 3;
    public const int MaxNameLength = 40;

    private User() { }

    public User(string name, string email, string passwordHash)
    {
        var address = new Email(email);
        AddNotifications(address);

        var trimmed = (name ?? string.Empty).Trim();
        AddNotificationIf(trimmed.Length is < MinNameLength or > MaxNameLength, nameof(Name),
            $"O nome deve ter entre {MinNameLength} e {MaxNameLength} caracteres");

        Name = trimmed;
        Email = address.Value;
        PasswordHash = passwordHash ?? string.Empty;
    }

    /// <summary>
    /// The account the Auth:Bypass flag runs as. It deliberately carries no password
    /// hash, which is what keeps it unreachable from the login form.
    /// </summary>
    public static User Local(string name, string email) => new(name, email, string.Empty);

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>True when no password could ever match — the local bypass account.</summary>
    public bool IsPasswordless => string.IsNullOrEmpty(PasswordHash);

    public ICollection<Category> Categories { get; private set; } = new List<Category>();
    public ICollection<Account> Accounts { get; private set; } = new List<Account>();
    public ICollection<CategoryRule> CategoryRules { get; private set; } = new List<CategoryRule>();
    public ICollection<Transaction> Transactions { get; private set; } = new List<Transaction>();
    public ICollection<RecurringTransaction> Recurrences { get; private set; } = new List<RecurringTransaction>();
    public ICollection<Trade> Trades { get; private set; } = new List<Trade>();
    public ICollection<FixedIncomeInvestment> FixedIncome { get; private set; } = new List<FixedIncomeInvestment>();
    public ICollection<CorporateAction> CorporateActions { get; private set; } = new List<CorporateAction>();
    public ICollection<Payout> Payouts { get; private set; } = new List<Payout>();
    public ICollection<WatchItem> Watchlist { get; private set; } = new List<WatchItem>();
    public ICollection<PriceAlert> PriceAlerts { get; private set; } = new List<PriceAlert>();
    public ICollection<Budget> Budgets { get; private set; } = new List<Budget>();
    public UserSettings? Settings { get; private set; }

    /// <summary>Set once, by the service that owns hashing. The domain never sees a plain password.</summary>
    public void SetPasswordHash(string hash)
    {
        AddNotificationIf(string.IsNullOrWhiteSpace(hash), nameof(PasswordHash), "Hash de senha inválido");

        if (IsValid)
            PasswordHash = hash;
    }
}
