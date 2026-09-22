using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Domain.Entities;

public class User : Notifiable
{
    public const int MinNameLength = 3;
    public const int MaxNameLength = 40;

    /// <summary>Failed sign-ins tolerated before the account locks itself.</summary>
    public const int MaxFailedSignIns = 5;

    /// <summary>How long the account stays locked once it has counted <see cref="MaxFailedSignIns"/> failures.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

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

    /// <summary>Consecutive failures since the last success or the last lockout.</summary>
    public int FailedSignInCount { get; private set; }

    /// <summary>When the current lockout ends; null when the account was never locked.</summary>
    public DateTimeOffset? LockedOutUntil { get; private set; }

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

    /// <summary>Set by the service that owns hashing, on registration and on a password change. The domain never sees a plain password.</summary>
    public void SetPasswordHash(string hash)
    {
        AddNotificationIf(string.IsNullOrWhiteSpace(hash), nameof(PasswordHash), "Hash de senha inválido");

        if (IsValid)
            PasswordHash = hash;
    }

    public void Rename(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        AddNotificationIf(trimmed.Length is < MinNameLength or > MaxNameLength, nameof(Name),
            $"O nome deve ter entre {MinNameLength} e {MaxNameLength} caracteres");

        if (IsValid)
            Name = trimmed;
    }

    public void ChangeEmail(string email)
    {
        var address = new Email(email);
        AddNotifications(address);

        if (IsValid)
            Email = address.Value;
    }

    /// <summary>True while the account is locked. A lockout in the past is simply over.</summary>
    public bool IsLockedOut(DateTimeOffset now) => LockedOutUntil > now;

    /// <summary>What is left of the lockout, rounded up to the next whole minute for a message a person can act on.</summary>
    public TimeSpan LockoutRemaining(DateTimeOffset now)
    {
        if (!IsLockedOut(now))
            return TimeSpan.Zero;

        var remaining = LockedOutUntil!.Value - now;
        return TimeSpan.FromMinutes(Math.Ceiling(remaining.TotalMinutes));
    }

    /// <summary>
    /// Counts one wrong password. The counter is reset when it trips the lockout rather
    /// than left at the ceiling, so an account that comes back from a lockout gets a fresh
    /// set of attempts instead of locking again on the very next typo.
    /// </summary>
    public void RegisterFailedSignIn(DateTimeOffset now)
    {
        FailedSignInCount++;

        if (FailedSignInCount < MaxFailedSignIns)
            return;

        FailedSignInCount = 0;
        LockedOutUntil = now + LockoutDuration;
    }

    /// <summary>Clears the count and any expired lockout. Called on every successful sign-in.</summary>
    public void RegisterSuccessfulSignIn()
    {
        FailedSignInCount = 0;
        LockedOutUntil = null;
    }
}
