namespace SlideWorks.WebApi.Tests.Support;

// The fixture's fake clock, passed through, that counts the timers the pump arms per deck (the pump hands its timers the
// deck id as state), so a test can wait for the window's timer rather than guess when the pool thread arms it.
public sealed class TimerWatchingClock(TimeProvider inner) : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, int> _armed = new(StringComparer.Ordinal);
    private readonly List<(string DeckId, int Count, TaskCompletionSource Done)> _waiters = [];

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

    public override long GetTimestamp() => inner.GetTimestamp();

    public override long TimestampFrequency => inner.TimestampFrequency;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = inner.CreateTimer(callback, state, dueTime, period);
        if (state is string deckId)
        {
            lock (_gate)
            {
                var armed = _armed[deckId] = _armed.GetValueOrDefault(deckId) + 1;
                foreach (var waiter in _waiters.Where(w => w.DeckId == deckId && w.Count <= armed).ToList())
                {
                    waiter.Done.SetResult();
                    _waiters.Remove(waiter);
                }
            }
        }
        return timer;
    }

    public Task ArmedAsync(string deckId, int count)
    {
        lock (_gate)
        {
            if (_armed.GetValueOrDefault(deckId) >= count)
            {
                return Task.CompletedTask;
            }
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((deckId, count, done));
            return done.Task;
        }
    }
}
