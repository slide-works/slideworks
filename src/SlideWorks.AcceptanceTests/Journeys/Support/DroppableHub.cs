namespace SlideWorks.AcceptanceTests.Journeys;

// Lets a test drop one page's hub connection. An init script records the page's /hubs/deck WebSockets; DropAsync closes the
// open one from the page with a non-normal code (the server sees it close too) and holds the page's next negotiate requests,
// so SignalR stays in its reconnecting state until Release lets them through. Chromium's offline mode doesn't close an open
// WebSocket. Playwright's RouteWebSocketAsync would be the natural tool, but its 1.62 .NET binding throws on a page-side
// close that carries no code, which SignalR always sends, and that takes the whole Playwright connection down. Set it up
// before the page navigates.
public sealed class DroppableHub
{
    private const string RECORD_SOCKETS =
        """
        (() => {
          const Native = window.WebSocket;
          const sockets = [];
          window.__hubSockets = sockets;
          window.WebSocket = class extends Native {
            constructor(...args) {
              super(...args);
              if (String(args[0]).includes('/hubs/deck')) sockets.push(this);
            }
          };
        })()
        """;

    // 4000: not a normal closure, as a dropped network looks to the page. Returns how many were open.
    private const string CLOSE_SOCKETS =
        """
        () => {
          const open = window.__hubSockets.filter((s) => s.readyState === WebSocket.OPEN);
          for (const s of open) s.close(4000, 'dropped by the test');
          return open.length;
        }
        """;

    private readonly Lock _gate = new();
    private readonly IPage _page;
    private TaskCompletionSource _open = NewGate();

    private DroppableHub(IPage page)
    {
        _page = page;
        _open.SetResult();
    }

    public static async Task<DroppableHub> RouteAsync(IPage page)
    {
        var hub = new DroppableHub(page);
        await page.AddInitScriptAsync(RECORD_SOCKETS);
        await page.RouteAsync("**/hubs/deck/negotiate**", async route =>
        {
            Task open;
            lock (hub._gate)
            {
                open = hub._open.Task;
            }
            await open;
            await route.ContinueAsync();
        });
        return hub;
    }

    // Closes the page's open hub WebSocket and holds its reconnect at negotiate. Returns how many it closed.
    public async Task<int> DropAsync()
    {
        lock (_gate)
        {
            _open = NewGate();
        }
        return await _page.EvaluateAsync<int>(CLOSE_SOCKETS);
    }

    // Lets the held negotiate requests through, and every later one.
    public void Release()
    {
        lock (_gate)
        {
            _open.TrySetResult();
        }
    }

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
