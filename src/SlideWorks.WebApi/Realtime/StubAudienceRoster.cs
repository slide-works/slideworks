using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// TODO: replaced by the roster that counts distinct guests per session. It counts 1 and remembers nobody.
[RegisterSingleton(For = typeof(IAudienceRoster))]
public sealed class StubAudienceRoster : IAudienceRoster
{
    public int Join(string sessionId, string guestId, string connectionId) => 1;

    public void Leave(string connectionId)
    {
    }

    public int Count(string sessionId) => 1;
}
