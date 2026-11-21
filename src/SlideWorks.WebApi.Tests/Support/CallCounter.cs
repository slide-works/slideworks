namespace SlideWorks.WebApi.Tests.Support;

// Counts calls per key (a deck id, say) and lets a test wait for a count: the shared half of the call-counting decorators
// that sit over a fake publisher. The decorator over the publisher interface calls Record once per call, failed ones
// included, and a test awaits WhenCalledAsync(key, count) for "published N times". A test that expects no call asserts
// CallsFor is 0 after it has waited for something that did happen (another key's call). The wait is bounded with
// Task.WhenAny and throws TimeoutException, as AG0032 asks. The counter holds no clock: it counts what happens.
public sealed class CallCounter
{
    public static readonly TimeSpan DefaultLimit = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _failed = new(StringComparer.Ordinal);
    private readonly List<(string Key, int Count, TaskCompletionSource Done)> _waiters = [];

    public int CallsFor(string key)
    {
        lock (_gate)
        {
            return _calls.GetValueOrDefault(key);
        }
    }

    public int FailedFor(string key)
    {
        lock (_gate)
        {
            return _failed.GetValueOrDefault(key);
        }
    }

    // Counts one call for the key, a failed one when failed is true, and releases the waits it completes.
    public void Record(string key, bool failed = false)
    {
        lock (_gate)
        {
            var calls = _calls[key] = _calls.GetValueOrDefault(key) + 1;
            if (failed)
            {
                _failed[key] = _failed.GetValueOrDefault(key) + 1;
            }
            foreach (var waiter in _waiters.Where(w => w.Key == key && w.Count <= calls).ToList())
            {
                waiter.Done.SetResult();
                _waiters.Remove(waiter);
            }
        }
    }

    // Completes once the key has had count calls (at once when it already has). Not bounded: use WhenCalledAsync in a test.
    public Task CalledAsync(string key, int count)
    {
        lock (_gate)
        {
            if (_calls.GetValueOrDefault(key) >= count)
            {
                return Task.CompletedTask;
            }
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((key, count, done));
            return done.Task;
        }
    }

    // Waits, at most limit (10 seconds when null), until the key has had count calls, failed ones included. Throws
    // TimeoutException saying what was missing.
    public async Task WhenCalledAsync(string key, int count, TimeSpan? limit = null)
    {
        var wait = CalledAsync(key, count);
        var bound = limit ?? DefaultLimit;
        if (await Task.WhenAny(wait, Task.Delay(bound)) != wait)
        {
            throw new TimeoutException($"'{key}' had {CallsFor(key)} of {count} calls within {bound.TotalSeconds:0.#} seconds.");
        }
        await wait;
    }
}
