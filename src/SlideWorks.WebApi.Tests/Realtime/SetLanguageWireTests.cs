using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// DeckHub.SetLanguage on the hub's wire (SW-883 K5): a guest picks the language it reads the slides in. Runs against fake
// sessions and relay and the real roster, which decides whether the connection has joined. The stand-in relay answers the
// same state for every language, so these prove the call, its refusal and its shape, not a translation.
[TestFixture]
[CancelAfter(30000)]
public class SetLanguageWireTests : ApiTestFixture
{
    private int _codes = 731000;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeAudienceSessions.AddTo(services);
        FakeAudienceRelay.AddTo(services);
    };

    private FakeAudienceRelay Relay => Factory.Services.GetRequiredService<FakeAudienceRelay>();

    [Test]
    public async Task SetLanguage_WhenNotJoined_ShouldRefuseNotJoined()
    {
        var (deck, code) = await LiveDeckAsync();
        await using var guest = await GuestClient.ConnectAsync(Factory);

        var refused = await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th"));

        refused.Message.ShouldContain(DeckHub.NOT_JOINED, Case.Sensitive);
        Relay.LanguagesReadFor(deck).ShouldBeEmpty();
    }

    [Test]
    public async Task SetLanguage_WhenJoined_ShouldReturnState()
    {
        var (deck, code) = await LiveDeckAsync();
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        var state = await guest.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th");

        JsonShape.AssertMatches(state, AudienceStateSample(), "audience-events.json audienceState", ScreenShapes.AudienceState);
        state.GetProperty("type").GetString().ShouldBe("audienceState");
        state.GetProperty("seq").GetInt64().ShouldBe(9);
        state.GetRawText().ShouldNotContain(deck, Case.Sensitive);
        state.TryGetProperty("group", out _).ShouldBeFalse();
        var reads = Relay.LanguagesReadFor(deck);
        reads[^1].ShouldBe("th");
    }

    // A deck Sam owns, live under a fresh code, with the relay answering a state at seq 9.
    private async Task<(string Deck, string Code)> LiveDeckAsync()
    {
        var deck = await SharingApi.CreateDeckAsync(Client, "Translated talk");
        var code = Interlocked.Increment(ref _codes).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Factory.Services.GetRequiredService<FakeAudienceSessions>().Set(code, deck, $"s{code}", AudiencePhase.Live);
        Relay.Set(new AudienceState(deck, 9, AudiencePhase.Live, null, 0, null, null, null));
        return (deck, code);
    }

    private static JsonElement AudienceStateSample()
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "audience-events.json")));
        return contract.RootElement.GetProperty("audienceState").Clone();
    }
}
