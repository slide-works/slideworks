using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support.Timing;

// A hub filter a host adds with ConfigureTestServices: it keeps each open hub connection, so a test can abort them from
// the server and drive the client's reconnect without routing the browser's WebSocket. Opens counts every connection
// that opened (key OPENED) and, for a signed-in person, their own connections (key OpenedBy(email)), so a test waits
// for one person's window to reconnect with Opens.WhenCalledAsync(OpenedBy(email), n + 1) and another person's
// connection can't satisfy the wait. The person is the finest key the filter has: a second window or tab of the same
// person counts under the same key, so a test that waits on it keeps that person to one window. Until AbortAll is called
// it only records, so a host that adds it behaves as before.
public sealed class HubAborts : IHubFilter
{
    public const string OPENED = "opened";

    private readonly ConcurrentDictionary<string, HubCallerContext> _open = new(StringComparer.Ordinal);

    public CallCounter Opens { get; } = new();

    // The Opens key for the connections the person with this email opened.
    public static string OpenedBy(string email) => $"{OPENED} by {email}";

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await next(context);
        _open[context.Context.ConnectionId] = context.Context;
        Opens.Record(OPENED);
        if (context.Context.User?.FindFirst(SlideWorksClaims.EMAIL)?.Value is { } email)
        {
            Opens.Record(OpenedBy(email));
        }
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        _open.TryRemove(context.Context.ConnectionId, out _);
        await next(context, exception);
    }

    // Drops every open hub connection as a dropped network would, and says how many it dropped. It aborts the HTTP
    // connection under each hub connection, not the hub connection itself: HubCallerContext.Abort sends SignalR's close
    // message without allowReconnect, and a client that gets that stops for good instead of reconnecting.
    public int AbortAll()
    {
        var aborted = 0;
        foreach (var (id, connection) in _open)
        {
            if (_open.TryRemove(id, out _))
            {
                connection.GetHttpContext().ShouldNotBeNull($"hub connection {id} should run over HTTP").Abort();
                aborted++;
            }
        }
        return aborted;
    }
}
