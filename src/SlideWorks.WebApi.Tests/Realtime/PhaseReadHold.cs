namespace SlideWorks.WebApi.Tests.Realtime;

// Holds the result of the next IAudienceSessions.ForDeckAsync after the real read has finished, so a test decides what
// happens between a phase read and the answer built from it. Armed once; the next read after Arm is the held one.
public sealed class PhaseReadHold
{
    private readonly Lock _gate = new();
    private TaskCompletionSource? _entered;
    private TaskCompletionSource? _release;

    // The returned task completes when a read has finished and is being held.
    public Task Arm()
    {
        lock (_gate)
        {
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _entered.Task;
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            _release?.TrySetResult();
        }
    }

    // Called by the decorator after its read: waits here when this read is the armed one.
    public async Task HoldIfArmedAsync()
    {
        TaskCompletionSource? entered;
        TaskCompletionSource? release;
        lock (_gate)
        {
            (entered, release) = (_entered, _release);
            _entered = null;
        }
        if (entered != null && release != null)
        {
            entered.TrySetResult();
            await release.Task;
        }
    }
}
