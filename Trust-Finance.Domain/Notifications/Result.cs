namespace TrustFinance.Domain.Notifications;

/// <summary>
/// The outcome of an operation that can legitimately fail. Replaces throwing for
/// expected rejections, so a caller handles failure by reading a value rather than by
/// wrapping the call in try/catch and hoping it caught the right type.
/// </summary>
public class Result
{
    protected Result(bool success, IReadOnlyList<Notification> notifications)
    {
        Success = success;
        Notifications = notifications;
    }

    public bool Success { get; }
    public bool Failed => !Success;
    public IReadOnlyList<Notification> Notifications { get; }

    /// <summary>The messages alone, which is what a form needs to render.</summary>
    public List<string> Messages => [.. Notifications.Select(n => n.Message)];

    public static Result Ok() => new(true, []);

    public static Result Fail(string key, string message) =>
        new(false, [new Notification(key, message)]);

    /// <summary>Carries an invalid entity's own complaints outward unchanged.</summary>
    public static Result Fail(Notifiable source) => new(false, source.Notifications);

    /// <summary>Ok when the entity is valid, its notifications when it is not.</summary>
    public static Result From(Notifiable source) =>
        source.IsValid ? Ok() : Fail(source);
}

/// <summary>A <see cref="Result"/> that also carries what was produced.</summary>
public sealed class Result<T> : Result
{
    private Result(bool success, T? value, IReadOnlyList<Notification> notifications)
        : base(success, notifications) => Value = value;

    public T? Value { get; }

    public static Result<T> Ok(T value) => new(true, value, []);

    public static new Result<T> Fail(string key, string message) =>
        new(false, default, [new Notification(key, message)]);

    public static new Result<T> Fail(Notifiable source) => new(false, default, source.Notifications);
}
