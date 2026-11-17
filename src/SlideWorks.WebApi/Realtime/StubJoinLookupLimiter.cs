using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// TODO: replaced by the limiter that counts misses per caller. It allows everything.
[RegisterSingleton(For = typeof(IJoinLookupLimiter))]
public sealed class StubJoinLookupLimiter : IJoinLookupLimiter
{
    public bool Allow(string callerKey) => true;

    public void Miss(string callerKey)
    {
    }
}
