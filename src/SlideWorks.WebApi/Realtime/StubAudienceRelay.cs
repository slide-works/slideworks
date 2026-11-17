using System.Text.Json;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// TODO: replaced by the relay that follows SW-4's presentation state. It answers for any deck with one fixed state (slide 1
// of 1, step 0) and sends nothing. The phase comes from IAudienceSessions as the real relay's does, and is live when the
// sessions know nothing of the deck; Current is filled only while it is live.
[RegisterSingleton(For = typeof(IAudienceRelay))]
public sealed class StubAudienceRelay(IAudienceSessions sessions) : IAudienceRelay
{
    private static readonly JsonElement _slots = JsonDocument.Parse("""{"title":"Stub"}""").RootElement.Clone();

    public async Task<AudienceState?> CurrentAsync(string deckId)
    {
        var phase = (await sessions.ForDeckAsync(deckId))?.Phase ?? AudiencePhase.Live;
        var current = phase == AudiencePhase.Live ? new AudienceSlide(1, 1, "title", _slots, 0, 0, false) : null;
        return new AudienceState(deckId, 1, phase, current, 0, null);
    }

    public Task StartedAsync(string deckId) => Task.CompletedTask;

    public Task EndedAsync(string deckId) => Task.CompletedTask;
}
