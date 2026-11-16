using System.Collections.Concurrent;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// POST /oauth/token and /oauth/revoke are anonymous, and each failed redeem or refresh takes SQLite's write lock, so a
// flood from one address would stall every deck write. Each caller IP gets one budget per minute for both endpoints, on
// the app's clock. An agent redeems once and refreshes about hourly, so the budget only bites on a flood. Per process,
// like the rest of SlideWorks' in-memory state, and shaped like RegistrationLimiter.
[RegisterSingleton(Concrete = true)]
public class TokenLimiter(TimeProvider clock)
{
    public const int PER_MINUTE = 60;

    private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);

    // True when the caller may call now, and counts the attempt. When refused, retryAfter is how long until the caller's
    // window ends.
    public bool TryAcquire(string callerKey, out TimeSpan retryAfter)
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
        retryAfter = window.Count <= PER_MINUTE ? TimeSpan.Zero : window.Start + WINDOW - now;
        return retryAfter == TimeSpan.Zero;
    }

    private sealed record Window(DateTimeOffset Start, int Count);
}
