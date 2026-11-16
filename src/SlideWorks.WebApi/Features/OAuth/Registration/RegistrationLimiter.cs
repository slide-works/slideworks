using System.Collections.Concurrent;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// Registration is anonymous, so each caller IP gets a small budget per minute, on the app's clock. Per process, like
// the rest of SlideWorks' in-memory state.
[RegisterSingleton(Concrete = true)]
public class RegistrationLimiter(TimeProvider clock)
{
    public const int PER_MINUTE = 10;

    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);

    // True when the caller may register now, and counts the attempt.
    public bool TryAcquire(string callerKey)
    {
        var now = clock.GetUtcNow();
        if (_windows.Count > 10_000)
        {
            foreach (var stale in _windows.Where(w => now - w.Value.Start >= WINDOW).Select(w => w.Key).ToList())
            {
                _windows.TryRemove(stale, out _);
            }
        }
        var window = _windows.AddOrUpdate(
            callerKey,
            _ => new Window(now, 1),
            (_, current) => now - current.Start >= WINDOW ? new Window(now, 1) : current with { Count = current.Count + 1 });
        return window.Count <= PER_MINUTE;
    }

    private sealed record Window(DateTimeOffset Start, int Count);
}
