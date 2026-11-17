using System.Text.Json;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Realtime;

// TODO: replaced by the relay that follows SW-4's presentation state. It answers with one fixed state (slide 1 of 1,
// step 0) for a deck the sessions know, and sends nothing. The phase comes from IAudienceSessions as the real relay's
// does, and Current is filled only while it is live. IAudienceSessions is per request, so a scope is opened per call.
[RegisterSingleton(For = typeof(IAudienceRelay))]
public sealed class StubAudienceRelay(IServiceScopeFactory scopes) : IAudienceRelay
{
    private static readonly JsonElement _slots = JsonDocument.Parse("""{"title":"Stub"}""").RootElement.Clone();

    public async Task<AudienceState?> CurrentAsync(string deckId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var found = await scope.ServiceProvider.GetRequiredService<IAudienceSessions>().ForDeckAsync(deckId);
        if (found is null)
        {
            return null;
        }
        var current = found.Phase == AudiencePhase.Live ? new AudienceSlide(1, 1, "title", _slots, 0, 0, false) : null;
        return new AudienceState(deckId, 1, found.Phase, current, 0, null);
    }

    public Task StartedAsync(string deckId) => Task.CompletedTask;

    public Task EndedAsync(string deckId) => Task.CompletedTask;
}
