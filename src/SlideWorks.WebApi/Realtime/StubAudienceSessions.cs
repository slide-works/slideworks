using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// TODO: replaced by the real sessions, which read the deck's sessions and join code from the database. It finds nothing.
[RegisterSingleton(For = typeof(IAudienceSessions))]
public sealed class StubAudienceSessions : IAudienceSessions
{
    public Task<AudienceSessionRef?> FindAsync(string code) => Task.FromResult<AudienceSessionRef?>(null);

    public Task<AudienceSessionRef?> ForDeckAsync(string deckId) => Task.FromResult<AudienceSessionRef?>(null);
}
