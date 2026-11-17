using SlideWorks.WebApi.Features.Live;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Realtime;

// The real IAudienceSessions, with a PhaseReadHold on each phase read by deck: the read happens, then its result is held.
public sealed class HoldingSessions(AudienceSessions inner, PhaseReadHold hold) : IAudienceSessions
{
    public Task<AudienceSessionRef?> FindAsync(string code) => inner.FindAsync(code);

    public async Task<AudienceSessionRef?> ForDeckAsync(string deckId)
    {
        var found = await inner.ForDeckAsync(deckId);
        await hold.HoldIfArmedAsync();
        return found;
    }
}
