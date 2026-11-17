namespace SlideWorks.WebApi.Tests.OAuth.Token;

// The step a test runs inside an admin's revoke, after the agent tokens have been ended and before the person's access
// changes (InterleavedRevoke). One host-wide instance; it runs once, then clears itself.
public sealed class RevokeInterleave
{
    private Func<Task>? _step;

    public void Arm(Func<Task> step) => _step = step;

    public Task RunAsync() => Interlocked.Exchange(ref _step, null)?.Invoke() ?? Task.CompletedTask;
}
