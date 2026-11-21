using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SlideWorks.WebApi.Tests.Support;

// Records the log lines of the classes it replaces the logger of, as "Category: message". Call Replace<T> from ConfigureTestServices, then read Lines.
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();

    public IReadOnlyList<string> Lines => [.. _lines];

    // Serilog owns the logger factory and ignores ILoggerProvider registrations, so a test captures a class's log by
    // replacing its ILogger<T> in ConfigureTestServices.
    public static CapturingLoggerProvider Replace<T>(IServiceCollection services)
    {
        var provider = new CapturingLoggerProvider();
        services.AddSingleton<ILogger<T>>(new Typed<T>(provider.CreateLogger(typeof(T).FullName!)));
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

    private sealed class Typed<T>(ILogger inner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner.Log(logLevel, eventId, state, exception, formatter);
    }
}
