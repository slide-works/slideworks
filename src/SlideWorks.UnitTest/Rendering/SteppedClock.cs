namespace SlideWorks.UnitTest.Rendering;

// A clock the test moves by hand.
public sealed class SteppedClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
