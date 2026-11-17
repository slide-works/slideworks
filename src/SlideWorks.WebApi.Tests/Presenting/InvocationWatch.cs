using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace SlideWorks.WebApi.Tests.Presenting;

// A hub filter a fixture adds with ConfigureTestServices: it completes once the server has finished running a hub method
// for a connection, with the exception the method threw, or null when it returned. A test waits on it to read what a call
// did after its caller's connection closed, when the caller can no longer get the result.
public sealed class InvocationWatch : IHubFilter
{
    private readonly ConcurrentDictionary<(string ConnectionId, string Method), TaskCompletionSource<Exception?>> _done = new();

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var done = Completion(invocationContext.Context.ConnectionId, invocationContext.HubMethodName);
        try
        {
            var result = await next(invocationContext);
            done.TrySetResult(null);
            return result;
        }
        catch (Exception e)
        {
            done.TrySetResult(e);
            throw;
        }
    }

    // The exception the server's hub method threw for this connection, or null when it returned.
    public async Task<Exception?> FinishedAsync(string connectionId, string method)
    {
        var done = Completion(connectionId, method).Task;
        if (await Task.WhenAny(done, Task.Delay(TimeSpan.FromSeconds(10))) != done)
        {
            throw new TimeoutException($"The server did not finish {method} for {connectionId}.");
        }
        return await done;
    }

    private TaskCompletionSource<Exception?> Completion(string connectionId, string method)
        => _done.GetOrAdd((connectionId, method), _ => new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously));
}
