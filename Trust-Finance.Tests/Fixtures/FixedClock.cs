namespace TrustFinance.Tests.Fixtures;

/// <summary>A clock stopped at one local date, so "due today" means the same thing on every run.</summary>
public sealed class FixedClock(DateOnly today) : TimeProvider
{
    public DateOnly Today { get; set; } = today;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow()
        => new(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}
