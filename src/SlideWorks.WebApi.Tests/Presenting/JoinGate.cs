namespace SlideWorks.WebApi.Tests.Presenting;

// Holds one JoinPresentation mid-flight, for the tests of a connection that closes while its join is still running.
// Arm(deckId) makes the next access check for that deck wait inside the hub call until Release(). GatedDeckAccess passes
// every other check straight through.
public sealed class JoinGate
{
    private readonly Lock _lock = new();
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _armedFor;

    public void Arm(string deckId)
    {
        lock (_lock)
        {
            _armedFor = deckId;
        }
    }

    // Called by GatedDeckAccess before the real check. Waits only for the armed deck, once.
    public async Task PassAsync(string deckId)
    {
        lock (_lock)
        {
            if (_armedFor != deckId)
            {
                return;
            }
            _armedFor = null;
        }
        _entered.TrySetResult();
        await _released.Task;
    }

    // Completes once a held call has reached the gate.
    public async Task EnteredAsync()
    {
        if (await Task.WhenAny(_entered.Task, Task.Delay(TimeSpan.FromSeconds(10))) != _entered.Task)
        {
            throw new TimeoutException("No access check reached the gate.");
        }
    }

    public void Release() => _released.TrySetResult();
}
