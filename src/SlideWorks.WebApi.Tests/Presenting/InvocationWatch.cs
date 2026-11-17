using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace SlideWorks.WebApi.Tests.Presenting;

// A hub filter a fixture adds with ConfigureTestServices: it completes once the server has finished running a hub method
// for a connection, whether the method returned or threw. A test waits on it to read what a call did after its caller's
// connection closed, when the caller can no longer get the result.
public sealed class InvocationWatch : IHubFilter
{
    private readonly ConcurrentDictionary<(string ConnectionId, string Method), TaskCompletionSource> _done = new();

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        finally
        {
            Completion(invocationContext.Context.ConnectionId, invocationContext.HubMethodName).TrySetResult();
        }
    }

    public async Task FinishedAsync(string connectionId, string method)
    {
        var done = Completion(connectionId, method).Task;
        if (await Task.WhenAny(done, Task.Delay(TimeSpan.FromSeconds(10))) != done)
        {
            throw new TimeoutException($"The server did not finish {method} for {connectionId}.");
        }
    }

    private TaskCompletionSource Completion(string connectionId, string method)
        => _done.GetOrAdd((connectionId, method), _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
