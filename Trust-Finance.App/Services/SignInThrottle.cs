using Microsoft.Extensions.Caching.Memory;

namespace TrustFinance.App.Services;

/// <summary>
/// A ceiling on failed sign-ins per network address, above the per-account lockout.
///
/// The lockout on <see cref="TrustFinance.Domain.Entities.User"/> protects one account from
/// being guessed. It does nothing against the other shape of the same attack: one attempt
/// each against thousands of accounts, which never trips any of them. This counts failures
/// by where they came from instead of who they were aimed at, so a host that is working
/// through a list stops after twenty tries whether or not the accounts exist.
///
/// In memory on purpose: the app is one process against one file, and a counter that is
/// lost on restart costs an attacker a window they cannot force open anyway.
/// </summary>
public sealed class SignInThrottle(IMemoryCache cache, TimeProvider clock)
{
    public const int MaxFailuresPerAddress = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly Lock _gate = new();

    /// <summary>How long the address has to wait, or <see cref="TimeSpan.Zero"/> when it is free to try.</summary>
    public TimeSpan RetryAfter(string? address)
    {
        if (Key(address) is not { } key)
            return TimeSpan.Zero;

        lock (_gate)
        {
            if (!cache.TryGetValue<Attempts>(key, out var attempts) || attempts is null)
                return TimeSpan.Zero;

            var now = clock.GetUtcNow();
            if (attempts.Count < MaxFailuresPerAddress || attempts.Until <= now)
                return TimeSpan.Zero;

            return TimeSpan.FromMinutes(Math.Ceiling((attempts.Until - now).TotalMinutes));
        }
    }

    public bool IsBlocked(string? address) => RetryAfter(address) > TimeSpan.Zero;

    public void RegisterFailure(string? address)
    {
        if (Key(address) is not { } key)
            return;

        lock (_gate)
        {
            var now = clock.GetUtcNow();

            if (cache.TryGetValue<Attempts>(key, out var attempts) && attempts is not null && attempts.Until > now)
            {
                attempts.Count++;
                return;
            }

            // A fresh window. Whether it is still open is decided by Until against the
            // injected clock; the cache entry's own lifetime is housekeeping, and it is set
            // relative to now because MemoryCache expires entries against the system clock,
            // which a test's clock is free to disagree with.
            var opened = new Attempts { Count = 1, Until = now + Window };
            cache.Set(key, opened, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Window });
        }
    }

    /// <summary>Forgets an address, on the sign-in that proves it was not an attacker after all.</summary>
    public void Clear(string? address)
    {
        if (Key(address) is { } key)
            lock (_gate)
                cache.Remove(key);
    }

    // No address — a request from a unix socket, or a test — is not a bucket anything can be
    // counted into, and refusing to sign anyone in would be worse than not counting.
    private static string? Key(string? address)
        => string.IsNullOrWhiteSpace(address) ? null : $"signin:{address}";

    private sealed class Attempts
    {
        public int Count;
        public DateTimeOffset Until;
    }
}
