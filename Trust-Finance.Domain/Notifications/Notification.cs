namespace TrustFinance.Domain.Notifications;

/// <summary>One thing that is wrong, and which field it is wrong about.</summary>
public sealed record Notification(string Key, string Message);
