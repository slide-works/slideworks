using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace SlideWorks.WebApi.Tests.Support.Timing;

// A hub filter a fixture adds with ConfigureTestServices: it completes a connection's task once the server has finished
// handling that connection's disconnect (DeckHub.OnDisconnectedAsync, and so IPresentations.LeaveAllAsync). A test waits
// on it to prove what a late disconnect did, without depending on the order of other messages.
public sealed class DisconnectWatch : IHubFilter
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _done = new(StringComparer.Ordinal);

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        await next(context, exception);
        Completion(context.Context.ConnectionId).TrySetResult();
    }

    // The server-side connection id is the client's HubConnection.ConnectionId.
    public async Task DisconnectedAsync(string connectionId)
    {
        var done = Completion(connectionId).Task;
        if (await Task.WhenAny(done, Task.Delay(TimeSpan.FromSeconds(10))) != done)
        {
            throw new TimeoutException($"The server did not finish disconnecting {connectionId}.");
        }
    }

    private TaskCompletionSource Completion(string connectionId)
        => _done.GetOrAdd(connectionId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
