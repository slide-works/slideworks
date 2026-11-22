namespace SlideWorks.WebApi.Tests.Support.Timing;

// Holds one side of an operation at a named point until the test lets it go, so a race window can be opened on purpose
// instead of hoped for. A test-project decorator over a seam interface calls PassAsync(key) before the real call (see
// Presenting/GatedDeckAccess); every key but the armed one passes straight through. Arm(key) holds the next call for
// that key, once. EnteredAsync waits, bounded (AG0032), until the held call has reached the gate; Release lets it go.
// Release in the test's teardown too, so a test that fails part way never leaves a call held.
public sealed class Gate
{
    public static readonly TimeSpan DefaultLimit = TimeSpan.FromSeconds(10);

    private readonly Lock _lock = new();
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _armedFor;

    public void Arm(string key)
    {
        lock (_lock)
        {
            _armedFor = key;
        }
    }

    // Called by the decorator before the real call. Waits only for the armed key, once.
    public async Task PassAsync(string key)
    {
        lock (_lock)
        {
            if (_armedFor != key)
            {
                return;
            }
            _armedFor = null;
        }
        _entered.TrySetResult();
        await _released.Task;
    }

    // Completes once a held call has reached the gate; throws TimeoutException after limit (10 seconds when null).
    public async Task EnteredAsync(TimeSpan? limit = null)
    {
        var bound = limit ?? DefaultLimit;
        if (await Task.WhenAny(_entered.Task, Task.Delay(bound)) != _entered.Task)
        {
            throw new TimeoutException($"No call reached the gate within {bound.TotalSeconds:0.#} seconds.");
        }
    }

    public void Release() => _released.TrySetResult();
}
