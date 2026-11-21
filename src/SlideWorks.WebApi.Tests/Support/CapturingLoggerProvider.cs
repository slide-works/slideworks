using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SlideWorks.WebApi.Tests.Support;

// Records every log line the app writes, as "Category: message". Register it from ConfigureTestServices with Register, then read Lines.
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyList<string> Lines => [.. _lines];

    public static CapturingLoggerProvider Register(IServiceCollection services)
    {
        var provider = new CapturingLoggerProvider();
        services.AddSingleton<ILoggerProvider>(provider);
        return provider;
    }

    public ILogger CreateLogger(string categoryName) => new Capture(categoryName, _lines);

    public void Dispose()
    {
    }

    private sealed class Capture(string category, ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => lines.Enqueue($"{category}: {formatter(state, exception)}");
    }
}
