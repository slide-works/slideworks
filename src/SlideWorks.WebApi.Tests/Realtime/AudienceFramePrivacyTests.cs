using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// What a guest and the owner's audience view receive over the hub is exactly what audience-events.json says (J3, J4): the
// key set of each frame as it reached the client, never the deck id the server routes by, and nothing from the deck
// beyond the slide on screen. Frames are read as received (JsonElement), not through a record.
[TestFixture]
[CancelAfter(30000)]
public class AudienceFramePrivacyTests : ApiTestFixture
{
    private const string SECRET_NOTE = "secret speaker note 7f3a";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeAudienceSessions.AddTo(services);
        FakeAudienceRelay.AddTo(services);
    };

    [Test]
    public async Task Frames_WhenGuestFollowsALiveDeck_ShouldHaveExactlyTheContractKeys()
    {
        var deck = await SharingApi.CreateDeckAsync(Client, "Private title " + SECRET_NOTE);
        var state = new AudienceState(deck, 43, AudiencePhase.Live, SlideOf(), 7, null);
        Factory.Services.GetRequiredService<FakeAudienceSessions>().Set("123456", deck, "s1", AudiencePhase.Live);
        Factory.Services.GetRequiredService<FakeAudienceRelay>().Set(state);
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await using var owner = await HubReceiver.StartAsync(Factory, SignedInAs);
        await owner.Connection.InvokeAsync("WatchAudience", deck);
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", "123456");
        await bus.PublishAsync(state);
        await bus.PublishAsync(new AudienceEnded(deck));
        var count = await owner.NextAudienceAsync();
        var pushedState = await guest.NextAudienceAsync();
        var ended = await guest.NextAudienceAsync();

        AssertMatches(joined, "audienceState", ScreenShapes.AudienceState);
        AssertMatches(pushedState, "audienceState", ScreenShapes.AudienceState);
        AssertMatches(ended, "audienceEnded", new JsonShapeOptions());
        AssertMatches(count, "joinedCount", new JsonShapeOptions());
        foreach (var frame in new[] { joined, pushedState, ended, count })
        {
            frame.GetRawText().ShouldNotContain(deck, Case.Sensitive);
            frame.GetRawText().ShouldNotContain(SECRET_NOTE, Case.Sensitive);
        }
    }

    private static AudienceSlide SlideOf()
        => new(3, 12, "bullets", JsonDocument.Parse("""{"headline":"Slice it","items":["a","b"],"reveal":"one-by-one"}""").RootElement.Clone(), 3, 1, false);

    private static void AssertMatches(JsonElement frame, string sample, JsonShapeOptions options)
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "audience-events.json")));
        JsonShape.AssertMatches(frame, contract.RootElement.GetProperty(sample), $"audience-events.json {sample}", options);
    }
}
