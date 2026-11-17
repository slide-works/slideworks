namespace SlideWorks.AcceptanceTests.Journeys;

// Holds the page's /hubs/deck negotiate requests until Release, so a journey can make a change before the page has
// joined its deck. Route it before the page navigates.
public sealed class HeldNegotiate
{
    private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(15);

    private readonly object _gate = new();
    private readonly TaskCompletionSource _open = NewGate();
    private int _asked;
    private TaskCompletionSource _askedChanged = NewGate();

    private HeldNegotiate()
    {
    }

    public static async Task<HeldNegotiate> RouteAsync(IPage page)
    {
        var hub = new HeldNegotiate();
        await page.RouteAsync("**/hubs/deck/negotiate**", async route =>
        {
            Task open;
            lock (hub._gate)
            {
                hub._asked += 1;
                hub._askedChanged.TrySetResult();
                hub._askedChanged = NewGate();
                open = hub._open.Task;
            }
            await open;
            await route.ContinueAsync();
        });
        return hub;
    }

    // Lets every held negotiate request through, and every later one.
    public void Release() => _open.TrySetResult();

    // Completes once the page has sent its {count}th negotiate request.
    public async Task AskedAsync(int count)
    {
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                if (_asked >= count)
                {
                    return;
                }
                changed = _askedChanged.Task;
            }
            var first = await Task.WhenAny(changed, Task.Delay(TIMEOUT));
            (first == changed).ShouldBeTrue($"The page never sent negotiate request {count} to /hubs/deck.");
        }
    }

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
