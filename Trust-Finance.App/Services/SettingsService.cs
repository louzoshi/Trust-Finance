using Microsoft.EntityFrameworkCore;
using TrustFinance.Data;
using TrustFinance.Domain.Entities;

namespace TrustFinance.App.Services;

/// <summary>
/// Per-user preferences, created on first read. A user who has never opened the settings
/// screen behaves exactly like one holding every default, which keeps every caller free
/// of null checks.
/// </summary>
public class SettingsService(IDbContextFactory<TrustFinanceDbContext> factory)
{
    public async Task<UserSettings> GetAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();

        var settings = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings is not null)
            return settings;

        settings = new UserSettings { UserId = userId };
        db.UserSettings.Add(settings);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two circuits for the same user can race to create the row; the unique index
            // catches the loser, and by then the winner's row is the answer.
            db.ChangeTracker.Clear();
            return await db.UserSettings.AsNoTracking().FirstAsync(s => s.UserId == userId);
        }

        return settings;
    }

    public async Task SaveAsync(UserSettings updated)
    {
        await using var db = await factory.CreateDbContextAsync();

        var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == updated.UserId)
            ?? throw new KeyNotFoundException("Configurações não encontradas");

        settings.Theme = updated.Theme;
        settings.Density = updated.Density;
        settings.StartPage = updated.StartPage;
        settings.AccentColor = updated.AccentColor;
        settings.PrivacyMode = updated.PrivacyMode;
        settings.MarketDataToken = string.IsNullOrWhiteSpace(updated.MarketDataToken)
            ? null
            : updated.MarketDataToken.Trim();
        settings.QuoteRefreshMinutes = Math.Clamp(updated.QuoteRefreshMinutes, 1, 1440);
        settings.NotifyPriceAlerts = updated.NotifyPriceAlerts;
        settings.NotifyBudgetOverruns = updated.NotifyBudgetOverruns;
        settings.NotifyContributionGoal = updated.NotifyContributionGoal;
        settings.MonthlyContributionGoal = Math.Max(updated.MonthlyContributionGoal, 0m);
        settings.EmergencyFundGoal = Math.Max(updated.EmergencyFundGoal, 0m);

        await db.SaveChangesAsync();
    }
}
