using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Domain.Entities;

/// <summary>A ticker the user is following but does not necessarily own.</summary>
public class WatchItem : Notifiable
{
    private WatchItem() { }

    public WatchItem(string ticker, AssetClass assetClass, int userId, DateTime addedAt)
    {
        var symbol = new Ticker(ticker);
        AddNotifications(symbol);

        AddNotificationIf(userId <= 0, nameof(UserId), "Item sem dono");

        Ticker = symbol.Value;
        Class = assetClass;
        UserId = userId;
        AddedAt = addedAt;
    }

    public int Id { get; private set; }
    public string Ticker { get; private set; } = string.Empty;
    public AssetClass Class { get; private set; }
    public DateTime AddedAt { get; private set; }

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;
}
