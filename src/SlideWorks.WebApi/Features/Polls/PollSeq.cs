using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.Polls;

// The one source of PollControlView.Seq (V4), for route responses and the publisher alike: strictly increasing and floored
// to the clock, like the audience feed's, so it keeps rising across a restart and a late view can always be dropped.
[RegisterSingleton]
public sealed class PollSeq(TimeProvider clock)
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
