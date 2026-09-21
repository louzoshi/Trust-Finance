namespace TrustFinance.Domain.Entities;

public enum Density
{
    Comfortable = 1,
    Compact = 2
}

public enum StartPage
{
    Dashboard = 1,
    Investments = 2,
    Transactions = 3
}

/// <summary>
/// Per-user preferences. One row per user, created on demand — a user who never opens
/// the settings screen still behaves like one holding every default.
/// </summary>
public class UserSettings
{
    public int Id { get; set; }

    /// <summary>"light", "dark", or null to follow whatever the browser chose.</summary>
    public string? Theme { get; set; }

    public Density Density { get; set; } = Density.Comfortable;
    public StartPage StartPage { get; set; } = StartPage.Dashboard;

    /// <summary>The accent the UI is built around, as a hex colour.</summary>
    public string? AccentColor { get; set; }

    /// <summary>Hide balances behind a blur, for opening the app where someone can see the screen.</summary>
    public bool PrivacyMode { get; set; }

    /// <summary>brapi.dev token. Empty means the app runs on sample market data.</summary>
    public string? MarketDataToken { get; set; }

    /// <summary>Minutes between quote refreshes. The free market-data tiers are rate limited.</summary>
    public int QuoteRefreshMinutes { get; set; } = 15;

    public bool NotifyPriceAlerts { get; set; } = true;
    public bool NotifyBudgetOverruns { get; set; } = true;
    public bool NotifyContributionGoal { get; set; } = true;

    /// <summary>What the user aims to invest each month. Zero means no target set.</summary>
    public decimal MonthlyContributionGoal { get; set; }

    /// <summary>Target size of the emergency reserve, in cash.</summary>
    public decimal EmergencyFundGoal { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
