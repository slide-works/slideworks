using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Join;

// The setup the guest-side server tests share: IAudienceSessions and IAudienceRelay are T0.1's settable fakes, so these tests
// don't need the real sessions, and the limiter records instead of counting. A test sets the code it uses with Open.
public abstract class JoinFixture : ApiTestFixture
{
    protected const string CODE = "482913";
    protected const string SESSION = "11111111-1111-1111-1111-111111111111";

    protected RecordingJoinLookupLimiter Limiter { get; } = new();

    protected FakeAudienceSessions Sessions => Factory.Services.GetRequiredService<FakeAudienceSessions>();

    protected FakeAudienceRelay Relay => Factory.Services.GetRequiredService<FakeAudienceRelay>();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeAudienceSessions.AddTo(services);
        FakeAudienceRelay.AddTo(services);
        services.AddSingleton<IJoinLookupLimiter>(Limiter);
    };

    // The code finds the deck in a live session, and the relay says the room is on slide 1 of 1.
    protected void Open(string code, string deckId)
    {
        Sessions.Set(code, deckId, SESSION, AudiencePhase.Live);
        Relay.Set(new AudienceState(deckId, 1, AudiencePhase.Live, new AudienceSlide(1, 1, "title", Slots("""{"title":"Ship smaller"}"""), 0, 0, false), 0, null));
    }

    protected static JsonElement Slots(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
