using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Join;

// IJoinLookupLimiter for tests of the guest lookup: it records the misses it was told about (through Miss or RecordMiss), and
// refuses every caller once Refuse is set, as the real limiter does after ten misses.
public sealed class RecordingJoinLookupLimiter : IJoinLookupLimiter
{
    private int _misses;

    public bool Refuse { get; set; }

    public int Misses => Volatile.Read(ref _misses);

    public bool Allow(string callerKey) => !Refuse;

    public void Miss(string callerKey) => Interlocked.Increment(ref _misses);

    public void Hit(string callerKey)
    {
    }

    public bool OverMissLimit(string callerKey) => Refuse;

    public void RecordMiss(string callerKey) => Interlocked.Increment(ref _misses);
}
