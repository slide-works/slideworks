using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.Timing;

namespace SlideWorks.WebApi.Tests.Presenting;

// P7 through set_talk_length: the same path as PUT /api/decks/{id}/talk-length, so the same rules. The activity row names
// the tool and the agent, and get_deck shows the talk length.
[TestFixture]
public class SetTalkLengthToolTests : ApiTestFixture
{
    private readonly TalkLengthCalls _presentations = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IPresentations>(_presentations);

    [Test]
    public async Task SetTalkLength_WhenOwnerSets30_ShouldShowInGetDeckRecordTheToolAndPublishDeckChanged()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var events = DeckEventsUntilMarker.Subscribe(Factory.Services.GetRequiredService<IDeckEvents>(), deck);

        var result = await SetAsync(agent, new { deck, minutes = 30 });

        result.GetProperty("talk_minutes").GetInt32().ShouldBe(30);
        (await GetDeckAsync(agent, deck)).GetProperty("talk_minutes").GetInt32().ShouldBe(30);
        var entry = (await DeckSeed.ActivityAsync(Client, deck))[0];
        entry.GetProperty("summary").GetString().ShouldBe("set the talk length to 30 min");
        entry.GetProperty("tool").GetString().ShouldBe("set_talk_length");
        entry.GetProperty("via").GetString().ShouldBe("mcp");
        entry.GetProperty("actorKind").GetString().ShouldBe("agent");
        entry.GetProperty("actor").GetString().ShouldBe("Claude");
        (await events.BeforeMarkerAsync()).ShouldHaveSingleItem().ShouldBeOfType<DeckChanged>().DeckId.ShouldBe(deck);
        _presentations.For(deck).ShouldBe([30]);
    }

    [Test]
    public async Task SetTalkLength_WhenNull_ShouldClearIt()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await SetAsync(agent, new { deck, minutes = 25 });

        var result = await SetAsync(agent, new { deck, minutes = (int?)null });

        result.GetProperty("talk_minutes").ValueKind.ShouldBe(JsonValueKind.Null);
        (await GetDeckAsync(agent, deck)).GetProperty("talk_minutes").ValueKind.ShouldBe(JsonValueKind.Null);
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe("cleared the talk length");
    }

    [Test]
    public async Task GetDeck_WhenNoTalkLengthIsSet_ShouldShowNull()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        (await GetDeckAsync(agent, deck)).GetProperty("talk_minutes").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task SetTalkLength_WhenRefused_ShouldGiveATextAndChangeNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        using var events = DeckEventsUntilMarker.Subscribe(Factory.Services.GetRequiredService<IDeckEvents>(), deck);

        (await SlideCalls.ErrorAsync(agent, "set_talk_length", new { deck, minutes = 0 })).ShouldBe("minutes: must be a whole number from 1 to 600, or null to clear the talk length");
        (await SlideCalls.ErrorAsync(agent, "set_talk_length", new { deck, minutes = 601 })).ShouldBe("minutes: must be a whole number from 1 to 600, or null to clear the talk length");
        (await SlideCalls.ErrorAsync(agent, "set_talk_length", new { deck, minutes = "30" })).ShouldBe("minutes: must be a whole number");
        (await SlideCalls.ErrorAsync(agent, "set_talk_length", new { deck })).ShouldBe("minutes: required");
        (await SlideCalls.ErrorAsync(agent, "set_talk_length", new { deck, minutes = 30, extra = 1 })).ShouldBe("extra: unknown argument");

        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
        (await events.BeforeMarkerAsync()).ShouldBeEmpty();
        (await GetDeckAsync(agent, deck)).GetProperty("talk_minutes").ValueKind.ShouldBe(JsonValueKind.Null);
        _presentations.For(deck).ShouldBeEmpty();
    }

    // The range check runs after the owner check (D16), so a non-owner hears "Deck not found" whatever the value.
    [TestCase(30)]
    [TestCase(0)]
    [TestCase(601)]
    public async Task SetTalkLength_WhenNotTheOwner_ShouldSayDeckNotFoundAndChangeNothing(int minutes)
    {
        await using var sam = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);
        var deck = await DeckSeed.CreateDeckAsync(sam);
        using var events = DeckEventsUntilMarker.Subscribe(Factory.Services.GetRequiredService<IDeckEvents>(), deck);

        (await SlideCalls.ErrorAsync(alex, "set_talk_length", new { deck, minutes })).ShouldBe("Deck not found");

        (await GetDeckAsync(sam, deck)).GetProperty("talk_minutes").ValueKind.ShouldBe(JsonValueKind.Null);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(1);
        (await events.BeforeMarkerAsync()).ShouldBeEmpty();
        _presentations.For(deck).ShouldBeEmpty();
    }

    private static async Task<JsonElement> SetAsync(McpTestClient agent, object args)
    {
        var (isError, json) = await agent.CallToolAsync("set_talk_length", args);
        isError.ShouldBeFalse(json.ToString());
        return json;
    }

    private static Task<JsonElement> GetDeckAsync(McpTestClient agent, string deck) => SlideCalls.OkAsync(agent, "get_deck", new { deck });
}
