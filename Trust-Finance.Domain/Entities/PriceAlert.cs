using TrustFinance.Domain.Notifications;
using TrustFinance.Domain.ValueObjects;

namespace TrustFinance.Domain.Entities;

public enum AlertDirection
{
    /// <summary>Fire when the price rises to or above the target — a sell signal, usually.</summary>
    Above = 1,

    /// <summary>Fire when the price falls to or below the target — a buying opportunity.</summary>
    Below = 2
}

public class PriceAlert : Notifiable
{
    private PriceAlert() { }

    public PriceAlert(string ticker, AssetClass assetClass, AlertDirection direction, decimal targetPrice, int userId)
    {
        var symbol = new Ticker(ticker);
        AddNotifications(symbol);

        AddNotificationIf(targetPrice <= 0, nameof(TargetPrice), "O preço-alvo precisa ser maior que zero");
        AddNotificationIf(!Enum.IsDefined(direction), nameof(Direction), "Condição inválida");
        AddNotificationIf(userId <= 0, nameof(UserId), "Alerta sem dono");

        Ticker = symbol.Value;
        Class = assetClass;
        Direction = direction;
        TargetPrice = targetPrice;
        UserId = userId;
    }

    public int Id { get; private set; }
    public string Ticker { get; private set; } = string.Empty;
    public AssetClass Class { get; private set; }
    public AlertDirection Direction { get; private set; }
    public decimal TargetPrice { get; private set; }

    /// <summary>Null while still armed. Stamped when the price first crosses, so it fires once and not on every refresh.</summary>
    public DateTime? TriggeredAt { get; private set; }

    /// <summary>The price that tripped it, kept so the notification can say what actually happened.</summary>
    public decimal? TriggeredPrice { get; private set; }

    public bool Dismissed { get; private set; }

    public int UserId { get; private set; }
    public User User { get; private set; } = null!;

    public bool IsArmed => TriggeredAt is null;

    /// <summary>Whether <paramref name="price"/> satisfies this alert's condition.</summary>
    public bool WouldTriggerAt(decimal price) =>
        Direction == AlertDirection.Above ? price >= TargetPrice : price <= TargetPrice;

    /// <summary>
    /// Arms down: records the crossing, once. Calling it again is a no-op, which is what
    /// stops a re-evaluation from resurrecting a notification the user already dismissed.
    /// </summary>
    public bool Trigger(decimal price, DateTime at)
    {
        if (!IsArmed || !WouldTriggerAt(price))
            return false;

        TriggeredAt = at;
        TriggeredPrice = price;
        return true;
    }

    public void Dismiss() => Dismissed = true;
}
