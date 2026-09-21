namespace TrustFinance.Domain.Notifications;

/// <summary>
/// Anything that can explain why it is invalid instead of throwing.
///
/// A rejected transaction is an expected outcome, not an exceptional one: the user typed
/// a negative amount and has to be told. Exceptions are for the cases nobody planned for,
/// and using them for validation means the caller writes try/catch around ordinary
/// business flow, loses every failure after the first, and pays a stack trace to render
/// a red label.
///
/// Deliberately hand-rolled rather than taken from Flunt: it keeps the domain project at
/// zero dependencies, which is the property that makes it impossible to leak a framework
/// in here by accident. The surface is the same, so swapping to Flunt is a base-class
/// change if that is ever wanted.
/// </summary>
public abstract class Notifiable
{
    private readonly List<Notification> _notifications = [];

    public IReadOnlyList<Notification> Notifications => _notifications;

    public bool IsValid => _notifications.Count == 0;
    public bool IsInvalid => !IsValid;

    protected void AddNotification(string key, string message)
    {
        // The same complaint twice helps nobody.
        if (_notifications.Any(n => n.Key == key && n.Message == message))
            return;

        _notifications.Add(new Notification(key, message));
    }

    /// <summary>Adds <paramref name="message"/> when <paramref name="condition"/> holds. Reads as the rule it enforces.</summary>
    protected void AddNotificationIf(bool condition, string key, string message)
    {
        if (condition) AddNotification(key, message);
    }

    /// <summary>Absorbs another notifiable's complaints, for an entity built out of value objects.</summary>
    protected void AddNotifications(Notifiable other)
    {
        foreach (var notification in other.Notifications)
            AddNotification(notification.Key, notification.Message);
    }

    protected void ClearNotifications() => _notifications.Clear();

    /// <summary>Every message joined, for a log line or a single-line error.</summary>
    public string Summary() => string.Join(" ", _notifications.Select(n => n.Message));
}
