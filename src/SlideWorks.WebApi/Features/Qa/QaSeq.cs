using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Qa;

// The one source of QaControlView.Seq and of every list revision (Q5), as PollSeq is for polls: strictly increasing and
// floored to the clock, so it keeps rising across a restart and a late view or an old revision can always be dropped.
[RegisterSingleton]
public sealed class QaSeq(TimeProvider clock)
{
    private long _last;

    public long Next()
    {
        var floor = clock.GetUtcNow().ToUnixTimeMilliseconds();
        long current;
        long next;
        do
        {
            current = Interlocked.Read(ref _last);
            next = Math.Max(current + 1, floor);
        }
        while (Interlocked.CompareExchange(ref _last, next, current) != current);
        return next;
    }
}
