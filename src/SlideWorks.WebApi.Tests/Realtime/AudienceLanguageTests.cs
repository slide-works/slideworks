using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Realtime;

// Per-language audience state over DeckHub (SW-883 K5, D11): SetLanguage, IAudienceRelay.CurrentAsync in a language, and one
// state per language in use, each to its own slide-state group. The real relay, sessions, roster and hub; SW-4's registry is
// stood in by a settable IPresentations, and a test moves the presenter by setting a state and calling the observers.
// Translations are seeded with TranslationRowSeed (D21), and a slide edit (add_slide) stands in for set_translations to make
// the feed compose again. Sam owns every deck. Slide 1 is a quote with a headline, slide 2 a Q&A slide.
[TestFixture]
[CancelAfter(60000)]
public class AudienceLanguageTests : ApiTestFixture
{
    private const string HEADLINE = "Slice it";
    private const string QUOTE = "Ship the smallest thing that works";
    private const string QUOTE_TH = "ส่งงานชิ้นที่เล็กที่สุดที่ใช้งานได้";
    private const string QUOTE_JA = "動く最小のものを出荷する";
    private const string QA_TITLE = "Questions from the room";
    private const string QA_TITLE_TH = "คำถามจากผู้ฟัง";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<SettablePresentations>().AddSingleton<IPresentations>(provider => provider.GetRequiredService<SettablePresentations>());

    private SettablePresentations Presentations => Factory.Services.GetRequiredService<SettablePresentations>();

    private TranslationRowSeed Seed => new(Factory.Services);

    [Test]
    public async Task SetLanguage_WhenDeckOffersIt_ShouldReturnMergedState()
    {
        var (deck, code) = await GoLiveAsync(await TranslatedDeckAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        var joined = await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        var state = await guest.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th");
        await MoveAsync(deck, slide: 1, step: 0);
        var pushed = await NextStateAsync(guest);

        joined.GetProperty("current").GetProperty("slots").GetProperty("quote").GetString().ShouldBe(QUOTE);
        joined.GetProperty("language").GetProperty("code").GetString().ShouldBe("en");
        state.GetProperty("current").GetProperty("slots").GetProperty("quote").GetString().ShouldBe(QUOTE_TH);
        // The headline and attribution have no Thai line, so they show in English and count as fallback lines.
        state.GetProperty("current").GetProperty("slots").GetProperty("headline").GetString().ShouldBe(HEADLINE);
        state.GetProperty("language").GetProperty("code").GetString().ShouldBe("th");
        state.GetProperty("language").GetProperty("fallbackLines").GetInt32().ShouldBe(2);
        state.GetProperty("seq").GetInt64().ShouldBeGreaterThan(joined.GetProperty("seq").GetInt64());
        state.TryGetProperty("group", out _).ShouldBeFalse();
        pushed.GetProperty("current").GetProperty("slots").GetProperty("quote").GetString().ShouldBe(QUOTE_TH);
        pushed.GetRawText().ShouldNotContain(QUOTE);
    }

    // SW-633's pair test: the frame's language is the contract sample's, which B2's client fixture is built from.
    [Test]
    public async Task AudienceState_WhenReadingInThai_ShouldCarryTheContractSampleLanguage()
    {
        var (deck, code) = await GoLiveAsync(await TranslatedDeckAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await guest.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th");

        await MoveAsync(deck, slide: 1, step: 0);
        var pushed = await NextStateAsync(guest);

        var sample = AudienceStateSample();
        JsonShape.AssertMatches(pushed, sample, "audience-events.json audienceState", ScreenShapes.AudienceState);
        JsonElement.DeepEquals(pushed.GetProperty("language"), sample.GetProperty("language")).ShouldBeTrue(pushed.GetProperty("language").GetRawText());
    }

    [Test]
    public async Task SetLanguage_WhenNotOffered_ShouldReturnPrimary()
    {
        var (deck, code) = await GoLiveAsync(await TranslatedDeckAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);

        var state = await guest.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "ko");
        await MoveAsync(deck, slide: 1, step: 0);
        var pushed = await NextStateAsync(guest);

        state.GetProperty("current").GetProperty("slots").GetProperty("quote").GetString().ShouldBe(QUOTE);
        state.GetProperty("language").GetProperty("code").GetString().ShouldBe("en");
        state.GetProperty("language").GetProperty("fallbackLines").GetInt32().ShouldBe(0);
        pushed.GetProperty("current").GetProperty("slots").GetProperty("quote").GetString().ShouldBe(QUOTE);
    }

    [Test]
    public async Task SetLanguage_WhenJoinedADifferentDeck_ShouldRefuseNotJoined()
    {
        var (_, joinedCode) = await GoLiveAsync(await TranslatedDeckAsync());
        var (_, otherCode) = await GoLiveAsync(await TranslatedDeckAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", joinedCode);

        var refused = await Should.ThrowAsync<HubException>(() => guest.Connection.InvokeAsync<JsonElement>("SetLanguage", otherCode, "th"));

        refused.Message.ShouldContain(DeckHub.NOT_JOINED, Case.Sensitive);
    }

    // LeaveAudience takes the connection out of its language's slide-state group too. The guest then joins another deck, and
    // the first state it gets must be that deck's move (slide 2), not the left deck's (slide 1) published before it.
    [Test]
    public async Task LeaveAudience_WhenReadingInThai_ShouldStopTheStates()
    {
        var (left, leftCode) = await GoLiveAsync(await TranslatedDeckAsync());
        var (other, otherCode) = await GoLiveAsync(await TranslatedDeckAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", leftCode);
        await guest.Connection.InvokeAsync<JsonElement>("SetLanguage", leftCode, "th");

        await guest.Connection.InvokeAsync("LeaveAudience", leftCode);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", otherCode);
        await MoveAsync(left, slide: 1, step: 0);
        await MoveAsync(other, slide: 2);
        var first = await NextStateAsync(guest);

        first.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(2);
        first.GetProperty("language").GetProperty("code").GetString().ShouldBe("en");
    }

    [Test]
    public async Task Move_WhenTwoLanguagesInUse_ShouldSendEachGroupItsOwnSlots()
    {
        var (deck, code) = await GoLiveAsync(await TranslatedDeckAsync());
        await using var thai = await GuestClient.ConnectAsync(Factory);
        await using var english = await GuestClient.ConnectAsync(Factory);
        await thai.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await english.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await thai.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th");
        var published = Listen(deck);

        await MoveAsync(deck, slide: 1, step: 0);
        var thaiFrame = await NextStateAsync(thai);
        var englishFrame = await NextStateAsync(english);
        await MoveAsync(deck, slide: 2);
        var thaiNext = await NextStateAsync(thai);
        var englishNext = await NextStateAsync(english);
        var states = await StatesUpToAsync(published.Reader, thaiNext.GetProperty("seq").GetInt64());
        published.Writer.TryComplete();

        var raw = (Thai: thaiFrame.GetRawText(), English: englishFrame.GetRawText());
        raw.Thai.ShouldContain(QUOTE_TH);
        raw.Thai.ShouldNotContain(QUOTE);
        raw.English.ShouldContain(QUOTE);
        raw.English.ShouldNotContain(QUOTE_TH);
        thaiFrame.GetProperty("seq").GetInt64().ShouldBe(englishFrame.GetProperty("seq").GetInt64());
        // Each guest got exactly one state for the first move: the next one it read is the second move's.
        thaiNext.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(2);
        englishNext.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(2);
        var firstMove = states.Where(s => s.Seq == thaiFrame.GetProperty("seq").GetInt64()).Select(s => s.Group).ToList();
        firstMove.ShouldBe([AudienceSlideGroups.Name(deck, "en"), AudienceSlideGroups.Name(deck, "th")], ignoreOrder: true);
    }

    [Test]
    public async Task Move_WhenTranslationsOff_ShouldSendOneStateWithNullLanguage()
    {
        var deck = await DeckAsync();
        var (_, code) = await GoLiveAsync(deck);
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        var state = await guest.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th");
        var published = Listen(deck);

        await MoveAsync(deck, slide: 1, step: 0);
        var frame = await NextStateAsync(guest);
        await MoveAsync(deck, slide: 2);
        var next = await NextStateAsync(guest);
        var states = await StatesUpToAsync(published.Reader, next.GetProperty("seq").GetInt64());
        published.Writer.TryComplete();

        state.GetProperty("language").ValueKind.ShouldBe(JsonValueKind.Null);
        frame.GetProperty("language").ValueKind.ShouldBe(JsonValueKind.Null);
        frame.GetProperty("current").GetProperty("slots").GetProperty("quote").GetString().ShouldBe(QUOTE);
        next.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(2);
        states.Where(s => s.Seq == frame.GetProperty("seq").GetInt64()).Select(s => s.Group).ShouldBe([AudienceSlideGroups.Name(deck, "en")]);
    }

    [Test]
    public async Task Compose_WhenTranslationsTurnedOff_ShouldMoveEveryoneToPrimary()
    {
        var (deck, code) = await GoLiveAsync(await TranslatedDeckAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        var picked = await guest.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th");

        await Seed.SettingsAsync(deck, false, "en", "th", "ja");
        await using (var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam))
        {
            await DeckSeed.AddQuoteAsync(agent, deck, "A slide added while live.");
        }
        var afterEdit = await NextStateAsync(guest);
        await MoveAsync(deck, slide: 1, step: 0);
        var afterMove = await NextStateAsync(guest);

        picked.GetProperty("language").GetProperty("code").GetString().ShouldBe("th");
        afterEdit.GetProperty("language").ValueKind.ShouldBe(JsonValueKind.Null);
        afterEdit.GetProperty("current").GetProperty("slots").GetProperty("quote").GetString().ShouldBe(QUOTE);
        afterEdit.GetProperty("current").GetProperty("count").GetInt32().ShouldBe(3);
        afterMove.GetProperty("language").ValueKind.ShouldBe(JsonValueKind.Null);
        afterMove.GetProperty("seq").GetInt64().ShouldBeGreaterThan(afterEdit.GetProperty("seq").GetInt64());
    }

    [Test]
    public async Task PollTally_WhenGuestsInTwoLanguages_ShouldReachBoth()
    {
        var (deck, code) = await GoLiveAsync(await TranslatedDeckAsync());
        await using var thai = await GuestClient.ConnectAsync(Factory);
        await using var english = await GuestClient.ConnectAsync(Factory);
        await thai.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await english.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await thai.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th");
        var round = Guid.NewGuid().ToString();

        await Factory.Services.GetRequiredService<IAudienceEvents>().PublishAsync(new PollTally(deck, PollTallyTarget.Audience, round, 4, null));
        var thaiTally = await HubReceiver.NextPollEventAsync(thai.NextAudienceAsync);
        var englishTally = await HubReceiver.NextPollEventAsync(english.NextAudienceAsync);

        thaiTally.GetProperty("round").GetString().ShouldBe(round);
        englishTally.GetProperty("round").GetString().ShouldBe(round);
        thaiTally.GetProperty("answered").GetInt32().ShouldBe(4);
    }

    [Test]
    public async Task CurrentAsync_WhenLanguageGiven_ShouldReturnMergedState()
    {
        var (deck, _) = await GoLiveAsync(await TranslatedDeckAsync());
        var relay = Factory.Services.GetRequiredService<IAudienceRelay>();

        var thai = await relay.CurrentAsync(deck, "th");
        var japanese = await relay.CurrentAsync(deck, "ja");
        var primary = await relay.CurrentAsync(deck, null);
        var thaiAgain = await relay.CurrentAsync(deck, "th");

        thai.ShouldNotBeNull().Current.ShouldNotBeNull().Slots.GetProperty("quote").GetString().ShouldBe(QUOTE_TH);
        thai.Language.ShouldBe(new AudienceLanguage("th", 2));
        thai.Group.ShouldBe(AudienceSlideGroups.Name(deck, "th"));
        japanese.ShouldNotBeNull().Current.ShouldNotBeNull().Slots.GetProperty("quote").GetString().ShouldBe(QUOTE_JA);
        japanese.Language.ShouldBe(new AudienceLanguage("ja", 0));
        primary.ShouldNotBeNull().Current.ShouldNotBeNull().Slots.GetProperty("quote").GetString().ShouldBe(QUOTE);
        primary.Language.ShouldBe(new AudienceLanguage("en", 0));
        thaiAgain.ShouldNotBeNull().Current.ShouldNotBeNull().Slots.GetProperty("quote").GetString().ShouldBe(QUOTE_TH);
        thaiAgain.Seq.ShouldBeGreaterThan(primary.Seq);
    }

    [Test]
    public async Task Move_WhenQaSlideInThai_ShouldSendThaiHeading()
    {
        var (deck, code) = await GoLiveAsync(await TranslatedDeckAsync());
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        await guest.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th");

        await MoveAsync(deck, slide: 2);
        var frame = await NextStateAsync(guest);

        frame.GetProperty("current").GetProperty("layout").GetString().ShouldBe("qa");
        frame.GetProperty("current").GetProperty("slots").GetProperty("title").GetString().ShouldBe(QA_TITLE_TH);
        frame.GetProperty("language").GetProperty("fallbackLines").GetInt32().ShouldBe(0);
    }

    // translate_slides publishes DeckChanged, and the feed composes again on it: a guest reading in Thai gets the new line on
    // the slide on screen with no slide move.
    [Test]
    public async Task TranslateSlides_WhenGuestReadsThatLanguage_ShouldPushTheNewLineWithoutAMove()
    {
        var deck = await DeckAsync();
        await Seed.SettingsAsync(deck, true, "en", "th");
        var (_, code) = await GoLiveAsync(deck);
        await MoveAsync(deck, slide: 1, step: 0);
        await using var guest = await GuestClient.ConnectAsync(Factory);
        await guest.Connection.InvokeAsync<JsonElement>("JoinAudience", code);
        var before = await guest.Connection.InvokeAsync<JsonElement>("SetLanguage", code, "th");
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var (_, read) = await agent.CallToolAsync("get_deck", new { deck });
        var revision = read.GetProperty("slides").EnumerateArray().Single(s => s.GetProperty("number").GetInt32() == 1).GetProperty("revision").GetInt64();

        var (isError, written) = await agent.CallToolAsync("translate_slides", new
        {
            deck,
            language = "th",
            slides = new[] { new { slide = 1, revision, lines = new Dictionary<string, string?> { ["/quote"] = QUOTE_TH } } },
        });
        var pushed = await NextStateAsync(guest);

        isError.ShouldBeFalse(written.ToString());
        before.GetProperty("current").GetProperty("slots").GetProperty("quote").GetString().ShouldBe(QUOTE);
        pushed.GetProperty("current").GetProperty("index").GetInt32().ShouldBe(1);
        pushed.GetProperty("current").GetProperty("slots").GetProperty("quote").GetString().ShouldBe(QUOTE_TH);
        pushed.GetProperty("language").GetProperty("code").GetString().ShouldBe("th");
        pushed.GetProperty("language").GetProperty("fallbackLines").GetInt32().ShouldBe(2);
    }

    // Slide 1: a quote with a headline; slide 2: a Q&A slide with its title.
    private async Task<string> DeckAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, "Translated talk");
        await DeckSeed.AddSlideAsync(agent, deck, "quote", JsonSerializer.Serialize(new { headline = HEADLINE, quote = QUOTE, attribution = "Sam Rivera" }));
        await DeckSeed.AddSlideAsync(agent, deck, "qa", JsonSerializer.Serialize(new { title = QA_TITLE }));
        return deck;
    }

    // DeckAsync with translations on in th and ja: slide 1's quote in Thai (headline and attribution not), all of it in
    // Japanese, and slide 2's title in Thai.
    private async Task<string> TranslatedDeckAsync()
    {
        var deck = await DeckAsync();
        await Seed.SettingsAsync(deck, true, "en", "th", "ja");
        await Seed.LinesAsync(deck, 1, new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            ["th"] = new Dictionary<string, (string Text, string From)> { ["/quote"] = (QUOTE_TH, QUOTE) },
            ["ja"] = new Dictionary<string, (string Text, string From)>
            {
                ["/headline"] = ("切り分ける", HEADLINE),
                ["/quote"] = (QUOTE_JA, QUOTE),
                ["/attribution"] = ("サム・リベラ", "Sam Rivera"),
            },
        });
        await Seed.LinesAsync(deck, 2, new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            ["th"] = new Dictionary<string, (string Text, string From)> { ["/title"] = (QA_TITLE_TH, QA_TITLE) },
        });
        return deck;
    }

    private async Task<(string Deck, string Code)> GoLiveAsync(string deck)
    {
        var response = await Client.PostAsync($"/api/decks/{deck}/live", null);
        response.EnsureSuccessStatusCode();
        return (deck, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!);
    }

    // What SW-4's registry does on an accepted change: record the state, then tell every observer.
    private async Task MoveAsync(string deck, int slide, int step = 0)
    {
        var state = SettablePresentations.At(deck, slide, step);
        Presentations.Set(state);
        foreach (var observer in Factory.Services.GetServices<IPresentationObserver>())
        {
            await observer.OnChangedAsync(state);
        }
    }

    // The next audienceState the guest receives; anything else (a joined count, a tally) is skipped.
    private static async Task<JsonElement> NextStateAsync(GuestClient guest)
    {
        while (true)
        {
            var frame = await guest.NextAudienceAsync();
            if (frame.GetProperty("type").GetString() == "audienceState")
            {
                return frame;
            }
        }
    }

    // Every AudienceState published on the bus for the deck from now on, in publish order.
    private Channel<AudienceState> Listen(string deck)
    {
        var channel = Channel.CreateUnbounded<AudienceState>();
        var subscription = Factory.Services.GetRequiredService<IAudienceEvents>().Subscribe(deck, published =>
        {
            if (published is AudienceState state)
            {
                channel.Writer.TryWrite(state);
            }
            return Task.CompletedTask;
        });
        _subscriptions.Add(subscription);
        return channel;
    }

    // The states read until one at this seq has been seen, and any more at that seq already published with it.
    private static async Task<IReadOnlyList<AudienceState>> StatesUpToAsync(ChannelReader<AudienceState> reader, long seq)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var read = new List<AudienceState>();
        while (read.Count == 0 || read[^1].Seq < seq)
        {
            read.Add(await reader.ReadAsync(timeout.Token));
        }
        while (reader.TryRead(out var more))
        {
            read.Add(more);
        }
        return read;
    }

    private readonly List<IDisposable> _subscriptions = [];

    [TearDown]
    public void DropSubscriptions()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
        _subscriptions.Clear();
    }

    private static JsonElement AudienceStateSample()
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "audience-events.json")));
        return contract.RootElement.GetProperty("audienceState").Clone();
    }
}
