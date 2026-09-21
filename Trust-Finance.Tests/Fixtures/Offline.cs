using Microsoft.Extensions.Logging;

namespace TrustFinance.Tests.Fixtures;

/// <summary>
/// An HTTP client that refuses to leave the machine. Every provider in the app is
/// written to degrade rather than throw when a third party is unreachable, and this is
/// what lets a unit test exercise that path — and stay honest about not touching the
/// network.
/// </summary>
public sealed class OfflineHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new RefusingHandler())
    {
        BaseAddress = new Uri("https://offline.invalid/"),
        Timeout = TimeSpan.FromMilliseconds(50)
    };

    private sealed class RefusingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => throw new HttpRequestException("Offline by design.");
    }
}

public sealed class NullLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formatter) { }
}

public sealed class NullLoggerFactory : ILoggerFactory
{
    public void AddProvider(ILoggerProvider provider) { }
    public ILogger CreateLogger(string categoryName) => new NullLogger<object>();
    public void Dispose() { }
}
