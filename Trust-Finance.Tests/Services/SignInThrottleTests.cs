using Microsoft.Extensions.Caching.Memory;
using TrustFinance.App.Services;
using TrustFinance.Tests.Fixtures;

namespace TrustFinance.Tests.Services;

public class SignInThrottleTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly FixedClock _clock = new(new DateOnly(2026, 9, 21));
    private readonly SignInThrottle _throttle;

    public SignInThrottleTests() => _throttle = new SignInThrottle(_cache, _clock);

    public void Dispose() => _cache.Dispose();

    [Fact]
    public void An_Address_Under_The_Ceiling_Should_Be_Free_To_Try()
    {
        for (var attempt = 0; attempt < SignInThrottle.MaxFailuresPerAddress - 1; attempt++)
            _throttle.RegisterFailure("203.0.113.7");

        _throttle.IsBlocked("203.0.113.7").Should().BeFalse();
    }

    [Fact]
    public void An_Address_Over_The_Ceiling_Should_Be_Told_To_Wait()
    {
        for (var attempt = 0; attempt < SignInThrottle.MaxFailuresPerAddress; attempt++)
            _throttle.RegisterFailure("203.0.113.7");

        _throttle.IsBlocked("203.0.113.7").Should().BeTrue();
        _throttle.RetryAfter("203.0.113.7").Should().BePositive();
    }

    [Fact]
    public void One_Blocked_Address_Should_Not_Block_Another()
    {
        for (var attempt = 0; attempt < SignInThrottle.MaxFailuresPerAddress; attempt++)
            _throttle.RegisterFailure("203.0.113.7");

        _throttle.IsBlocked("198.51.100.4").Should().BeFalse();
    }

    [Fact]
    public void The_Window_Should_Close_On_Its_Own()
    {
        for (var attempt = 0; attempt < SignInThrottle.MaxFailuresPerAddress; attempt++)
            _throttle.RegisterFailure("203.0.113.7");

        _clock.Today = _clock.Today.AddDays(1);

        _throttle.IsBlocked("203.0.113.7").Should().BeFalse();
    }

    [Fact]
    public void A_Successful_SignIn_Should_Forget_The_Address()
    {
        for (var attempt = 0; attempt < SignInThrottle.MaxFailuresPerAddress; attempt++)
            _throttle.RegisterFailure("203.0.113.7");

        _throttle.Clear("203.0.113.7");

        _throttle.IsBlocked("203.0.113.7").Should().BeFalse();
    }

    [Fact]
    public void A_Request_With_No_Address_Should_Never_Be_Blocked()
    {
        // A unix socket or a test has nothing to count against, and refusing every sign-in
        // would be a worse answer than not counting.
        for (var attempt = 0; attempt < SignInThrottle.MaxFailuresPerAddress * 2; attempt++)
            _throttle.RegisterFailure(null);

        _throttle.IsBlocked(null).Should().BeFalse();
    }
}
