using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Support;

// IAudienceSessions for tests that need a session without opening one: each test sets the codes it uses with Set. Add it
// in ConfigureTestServices with FakeAudienceSessions.AddTo, and read the instance back from the factory's services to set
// it. A code or deck that was never set finds nothing, as the real sessions do for an unknown code.
public sealed class FakeAudienceSessions : IAudienceSessions
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AudienceSessionRef> _byCode = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AudienceSessionRef> _byDeck = new(StringComparer.Ordinal);

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeAudienceSessions>().AddSingleton<IAudienceSessions>(provider => provider.GetRequiredService<FakeAudienceSessions>());

    // The code now finds the deck in this phase; the session id is for a live phase and null otherwise.
    public void Set(string code, string deckId, string? sessionId, AudiencePhase phase)
    {
        var found = new AudienceSessionRef(deckId, sessionId, phase);
        lock (_gate)
        {
            _byCode[code] = found;
            _byDeck[deckId] = found;
        }
    }

    // The code finds nothing again.
    public void Clear(string code)
    {
        lock (_gate)
        {
            if (_byCode.Remove(code, out var found))
            {
                _byDeck.Remove(found.DeckId);
            }
        }
    }

    public Task<AudienceSessionRef?> FindAsync(string code)
    {
        lock (_gate)
        {
            return Task.FromResult(_byCode.GetValueOrDefault(code));
        }
    }

    public Task<AudienceSessionRef?> ForDeckAsync(string deckId)
    {
        lock (_gate)
        {
            return Task.FromResult(_byDeck.GetValueOrDefault(deckId));
        }
    }
}
