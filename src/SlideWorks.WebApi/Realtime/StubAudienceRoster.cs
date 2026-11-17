using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// TODO: replaced by the roster that counts distinct guests per session. It counts 1 and remembers nobody.
[RegisterSingleton(For = typeof(IAudienceRoster))]
public sealed class StubAudienceRoster : IAudienceRoster
{
    public Task<int> JoinAsync(string deckId, string sessionId, string guestId, string connectionId) => Task.FromResult(1);

    public Task<RosterLeave?> LeaveAsync(string connectionId) => Task.FromResult<RosterLeave?>(null);

    public Task<int> CountAsync(string sessionId) => Task.FromResult(1);
}
