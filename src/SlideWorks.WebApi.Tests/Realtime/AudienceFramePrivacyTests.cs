using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// What a guest and the owner's audience view receive over the hub is exactly what audience-events.json says (J3, J4): the
// key set of each frame as it reached the client, never the deck id the server routes by, and nothing from the deck
// beyond the slide on screen. Frames are read as received (JsonElement), not through a record. This runs through the fake
// relay, so it proves shape only; the value tests (notes, timer, deep dives, emails) are in AudienceRelayTests, through
// the real relay.
[TestFixture]
[CancelAfter(30000)]
public class AudienceFramePrivacyTests : ApiTestFixture
{
    // The keys a room may carry to a guest (qa-room.json, SW-9 Q1): nothing about questions, guests or votes.
    private static readonly string[] _roomKeys = ["round", "questions", "names", "reactions", "listRevision"];

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        FakeAudienceSessions.AddTo(services);
        FakeAudienceRelay.AddTo(services);
    };

    [Test]
    public async Task Frames_WhenGuestFollowsALiveDeck_ShouldHaveExactlyTheContractKeys()
    {
        var deck = await SharingApi.CreateDeckAsync(Client, "Private title");
        var state = new AudienceState(deck, 43, AudiencePhase.Live, SlideOf(), 7, null, RoomOf(), null);
        Factory.Services.GetRequiredService<FakeAudienceSessions>().Set("123456", deck, "s1", AudiencePhase.Live);
        Factory.Services.GetRequiredService<FakeAudienceRelay>().Set(state);
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await using var owner = await HubReceiver.StartAsync(Factory, SignedInAs);
        await owner.Connection.InvokeAsync("WatchAudience", deck);
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", "123456");
        // To the primary's slide-state group, where JoinAudience put the guest (SW-883 D11).
        await bus.PublishAsync(state with { Group = AudienceSlideGroups.Name(deck, "en") });
        await bus.PublishAsync(new AudienceEnded(deck));
        var count = await owner.NextAudienceAsync();
        var pushedState = await guest.NextAudienceAsync();
        var ended = await guest.NextAudienceAsync();

        AssertMatches(joined, "audienceState", ScreenShapes.AudienceState);
        AssertMatches(pushedState, "audienceState", ScreenShapes.AudienceState);
        AssertMatches(ended, "audienceEnded", new JsonShapeOptions());
        AssertMatches(count, "joinedCount", new JsonShapeOptions());
        PollActivityKeys.Of(joined.GetProperty("activity")).ShouldBeSubsetOf(PollActivityKeys.All);
        PollActivityKeys.Of(pushedState.GetProperty("activity")).ShouldBeSubsetOf(PollActivityKeys.All);
        joined.GetProperty("room").EnumerateObject().Select(p => p.Name).ShouldBe(_roomKeys, ignoreOrder: true);
        pushedState.GetProperty("room").EnumerateObject().Select(p => p.Name).ShouldBe(_roomKeys, ignoreOrder: true);
        foreach (var frame in new[] { joined, pushedState, ended, count })
        {
            frame.GetRawText().ShouldNotContain(deck, Case.Sensitive);
        }
    }

    // A poll on screen: the activity in the frame has exactly PollActivity's keys, nothing more (no counts, opens, privacy
    // or anything about other guests). The activity is serialised as AudienceFeed does it, with the web defaults.
    [Test]
    public async Task Frames_WhenSlideHasPoll_ShouldCarryExactlyThePollActivityKeys()
    {
        var deck = await SharingApi.CreateDeckAsync(Client, "Private title");
        var activity = new PollActivity(Guid.NewGuid().ToString(), 2, PollPhase.Closed, "Which?", ["One", "Two"], PollPick.UpTo, 2, PollResults.AfterClose, true, 1);
        var state = new AudienceState(deck, 44, AudiencePhase.Live, SlideOf(), 7, JsonSerializer.SerializeToElement(activity, JsonSerializerOptions.Web), null, null);
        Factory.Services.GetRequiredService<FakeAudienceSessions>().Set("123457", deck, "s2", AudiencePhase.Live);
        Factory.Services.GetRequiredService<FakeAudienceRelay>().Set(state);
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var bus = Factory.Services.GetRequiredService<IAudienceEvents>();

        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", "123457");
        // To the primary's slide-state group, where JoinAudience put the guest (SW-883 D11).
        await bus.PublishAsync(state with { Group = AudienceSlideGroups.Name(deck, "en") });
        var pushed = await guest.NextAudienceAsync();

        foreach (var frame in new[] { joined, pushed })
        {
            // The sample's activity is null, so the frame's own keys are compared here and its activity against poll-activity.json.
            frame.EnumerateObject().Select(p => p.Name).ShouldBe(AudienceEventSample("audienceState").EnumerateObject().Select(p => p.Name), ignoreOrder: true);
            PollActivityKeys.Of(frame.GetProperty("activity")).ShouldBe(PollActivityKeys.All, ignoreOrder: true);
            JsonShape.AssertMatches(frame.GetProperty("activity"), PollActivitySample("answerShown"), "poll-activity.json answerShown", ScreenShapes.PollActivity);
            frame.GetRawText().ShouldNotContain(deck, Case.Sensitive);
        }
    }

    private static JsonElement PollActivitySample(string name) => SampleOf("poll-activity.json", name);

    private static JsonElement AudienceEventSample(string name) => SampleOf("audience-events.json", name);

    private static JsonElement SampleOf(string file, string name)
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));
        return contract.RootElement.GetProperty(name).Clone();
    }

    // A live room as AudienceFeed serialises it, with the web defaults.
    private static JsonElement RoomOf()
        => JsonSerializer.SerializeToElement(new QaRoom(Guid.NewGuid().ToString(), QaRoomQuestions.Open, true, true, 57), JsonSerializerOptions.Web);

    private static AudienceSlide SlideOf()
        => new(3, 12, "bullets", JsonDocument.Parse("""{"headline":"Slice it","items":["a","b"],"reveal":"one-by-one"}""").RootElement.Clone(), 3, 1, false);

    private static void AssertMatches(JsonElement frame, string sample, JsonShapeOptions options)
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "audience-events.json")));
        JsonShape.AssertMatches(frame, contract.RootElement.GetProperty(sample), $"audience-events.json {sample}", options);
    }
}
