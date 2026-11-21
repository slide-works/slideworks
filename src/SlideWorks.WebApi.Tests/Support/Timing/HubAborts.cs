using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace SlideWorks.WebApi.Tests.Support.Timing;

// A hub filter a host adds with ConfigureTestServices: it keeps each open hub connection, so a test can abort them from
// the server and drive the client's reconnect without routing the browser's WebSocket. Opens counts every connection
// that opened (key "opened"), so a test waits for the reconnect with Opens.WhenCalledAsync. Until AbortAll is called it
// only records, so a host that adds it behaves as before.
public sealed class HubAborts : IHubFilter
{
    public const string OPENED = "opened";

    private readonly ConcurrentDictionary<string, HubCallerContext> _open = new(StringComparer.Ordinal);

    public CallCounter Opens { get; } = new();

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await next(context);
        _open[context.Context.ConnectionId] = context.Context;
        Opens.Record(OPENED);
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        _open.TryRemove(context.Context.ConnectionId, out _);
        await next(context, exception);
    }

    // Aborts every open hub connection, as a dropped network or a server restart would, and says how many it aborted.
    public int AbortAll()
    {
        var aborted = 0;
        foreach (var (id, connection) in _open)
        {
            if (_open.TryRemove(id, out _))
            {
                connection.Abort();
                aborted++;
            }
        }
        return aborted;
    }
}
