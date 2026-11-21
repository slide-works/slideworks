namespace SlideWorks.WebApi.Tests.Support.Timing;

// The real clock, plus offsets a test can step: Advance moves wall and monotonic time together (as time passing does),
// StepWall moves the wall clock alone (as NTP or a VM's clock sync does). With no step it is TimeProvider.System, so a
// host that swaps it in behaves exactly as before. For a running app (the acceptance host), where a FakeTimeProvider
// would stop time for every journey; in WebApi.Tests prefer FakeTimeProvider, or WallStepClock to step the wall alone.
// Timers are the system's and do not see the offsets. Whoever steps it calls Reset in teardown, because the app is shared.
public sealed class StepClock : TimeProvider
{
    private readonly Lock _lock = new();
    private TimeSpan _elapsed;
    private TimeSpan _wall;

    public override TimeZoneInfo LocalTimeZone => System.LocalTimeZone;

    public override long TimestampFrequency => System.TimestampFrequency;

    // Moves both clocks forward by step.
    public void Advance(TimeSpan step)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(step, TimeSpan.Zero);
        lock (_lock)
        {
            _elapsed += step;
        }
    }

    // Moves the wall clock alone by step (back when negative).
    public void StepWall(TimeSpan step)
    {
        lock (_lock)
        {
            _wall += step;
        }
    }

    // Back to the real clock.
    public void Reset()
    {
        lock (_lock)
        {
            _elapsed = TimeSpan.Zero;
            _wall = TimeSpan.Zero;
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return System.GetUtcNow() + _elapsed + _wall;
        }
    }

    public override long GetTimestamp()
    {
        lock (_lock)
        {
            return System.GetTimestamp() + (long)(_elapsed.TotalSeconds * TimestampFrequency);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => System.CreateTimer(callback, state, dueTime, period);
}
