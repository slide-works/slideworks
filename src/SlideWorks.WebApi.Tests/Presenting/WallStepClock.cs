using Microsoft.Extensions.Time.Testing;

namespace SlideWorks.WebApi.Tests.Presenting;

// A clock whose wall time can step on its own, as NTP or a VM's clock sync steps it, while the monotonic timestamp keeps
// to the FakeTimeProvider it wraps. FakeTimeProvider ties the two together (its timestamp is its UTC time, and SetUtcNow
// refuses to go back), so it cannot express a wall clock that steps back with no monotonic time passing.
public sealed class WallStepClock(FakeTimeProvider inner) : TimeProvider
{
    private TimeSpan _wallOffset;

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override long TimestampFrequency => inner.TimestampFrequency;

    // Moves the wall clock by step (back when negative) without moving the timestamp.
    public void StepWall(TimeSpan step) => _wallOffset += step;

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow() + _wallOffset;

    public override long GetTimestamp() => inner.GetTimestamp();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => inner.CreateTimer(callback, state, dueTime, period);
}
