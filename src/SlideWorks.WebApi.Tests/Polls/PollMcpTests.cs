using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Polls;

// The poll tools an agent has (V9) and the option rules that guard them: update_poll, add_poll_option, open_poll, close_poll
// and get_poll_results over the wire, and update_slide's rule while a round is open (D13). Rounds and answers come from
// PollRowSeed; open_poll and close_poll go to FakePolls; the audience relay and the publisher are the recording fakes. The
// app, and so the fakes, is shared by the fixture: each test makes its own deck.
[TestFixture]
public class PollMcpTests : PollTestFixture
{
    private const string FOUR_OPTIONS = """{ "question": "Which part should we cut?", "options": ["The intro", "The demo", "The recap", "The close"] }""";

    private FakePolls Polls => Factory.Services.GetRequiredService<FakePolls>();

    private FakePollPublisher Publisher => Factory.Services.GetRequiredService<FakePollPublisher>();

    private FakeAudienceRelay Relay => Factory.Services.GetRequiredService<FakeAudienceRelay>();

    protected override void ConfigurePollServices(IServiceCollection services)
    {
        FakePolls.AddTo(services);
        FakePollPublisher.AddTo(services);
        FakeAudienceRelay.AddTo(services);
    }

    [TearDown]
    public void AcceptAgain() => Polls.Reset();

    [Test]
    public async Task UpdatePoll_WhenSettingsGiven_ShouldReturnThemKeepTheOthersAndLogTheTool()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, pick = "upTo", max = 2, results = "afterAnswer", correct = 3 });

        result.GetProperty("pick").GetString().ShouldBe("upTo");
        result.GetProperty("max").GetInt32().ShouldBe(2);
        result.GetProperty("results").GetString().ShouldBe("afterAnswer");
        result.GetProperty("correct").GetInt32().ShouldBe(3);
        result.GetProperty("opens").GetString().ShouldBe("onArrival");
        result.GetProperty("privacy").GetString().ShouldBe("private");
        (await SettingsAsync(poll)).ShouldBe(new PollSettings(PollPick.UpTo, 2, PollResults.AfterAnswer, 3, PollOpens.OnArrival, PollPrivacy.Private));
        var entry = (await DeckSeed.ActivityAsync(Client, poll.DeckId))[0];
        entry.GetProperty("tool").GetString().ShouldBe("update_poll");
        entry.GetProperty("summary").GetString().ShouldBe($"changed the settings of the poll on slide {poll.Slide}");
        Relay.Refreshed.ShouldContain(poll.DeckId);
        Publisher.PublishedFor(poll.DeckId).ShouldBe([poll.Slide]);
    }

    [Test]
    public async Task UpdatePoll_WhenPickGoesBackToOne_ShouldClearMaxAndZeroClearsCorrect()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await SlideCalls.OkAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, pick = "upTo", max = 3, correct = 1 });

        var result = await SlideCalls.OkAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, pick = "one", correct = 0, opens = "manual" });

        result.GetProperty("max").ValueKind.ShouldBe(JsonValueKind.Null);
        result.GetProperty("correct").ValueKind.ShouldBe(JsonValueKind.Null);
        (await SettingsAsync(poll)).ShouldBe(new PollSettings(PollPick.One, null, PollResults.Live, null, PollOpens.Manual, PollPrivacy.Private));
    }

    [Test]
    public async Task UpdatePoll_WhenRoundIsOpen_ShouldRefuseWithConflict()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var entries = (await DeckSeed.ActivityAsync(Client, poll.DeckId)).Count;

        var error = await SlideCalls.ErrorAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, results = "afterClose" });

        error.ShouldBe($"A round of the poll on slide {poll.Slide} is open, so its settings can't change; close it first (close_poll)");
        (await SettingsAsync(poll)).ShouldBe(PollSettings.Default);
        (await DeckSeed.ActivityAsync(Client, poll.DeckId)).Count.ShouldBe(entries);
        Relay.Refreshed.ShouldNotContain(poll.DeckId);
        Publisher.PublishedFor(poll.DeckId).ShouldBeEmpty();
    }

    // A draft has no answers to break and no phone has answered it, so settings may change under it; a closed round keeps its
    // Final. The change is announced, because the phones show a draft's pick, max and results.
    [Test]
    public async Task UpdatePoll_WhenOnlyADraftRoundExists_ShouldChangeTheSettings()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId, PollPhase.Draft);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, results = "afterClose" });

        result.GetProperty("results").GetString().ShouldBe("afterClose");
        Publisher.PublishedFor(poll.DeckId).ShouldBe([poll.Slide]);
    }

    [Test]
    public async Task UpdatePoll_WhenRoundIsClosed_ShouldChangeTheSettings()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "closed-round-guest", 1);
        await Rows.CloseAsync(round.Id, joinedAtClose: 3);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, correct = 2 });

        result.GetProperty("correct").GetInt32().ShouldBe(2);
    }

    [Test]
    public async Task UpdatePoll_WhenRoundIsOpenOnAnotherSlide_ShouldChangeTheSettings()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var second = (await DeckSeed.AddSlideAsync(agent, poll.DeckId, "poll", FOUR_OPTIONS)).GetProperty("slide").GetInt32();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);

        var result = await SlideCalls.OkAsync(agent, "update_poll", new { deck = poll.DeckId, slide = second, results = "afterClose" });

        result.GetProperty("results").GetString().ShouldBe("afterClose");
    }

    [Test]
    public async Task UpdatePoll_WhenMaxExceedsOptions_ShouldRefuseNamingMax()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var error = await SlideCalls.ErrorAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, pick = "upTo", max = 5 });

        error.ShouldBe("max: must be 2 to 3");
        (await SettingsAsync(poll)).ShouldBe(PollSettings.Default);
    }

    [TestCase("""{ "pick": "upTo" }""", "max: required when pick is upTo")]
    [TestCase("""{ "max": 2 }""", "max: only when pick is upTo")]
    [TestCase("""{ "correct": 4 }""", "correct: must be 1 to 3")]
    [TestCase("""{ "correct": -1 }""", "correct: must be 1 to 3")]
    [TestCase("""{ "pick": "many" }""", "pick: must be one of 'one', 'upTo'")]
    [TestCase("""{ "results": "never" }""", "results: must be one of 'live', 'afterAnswer', 'afterClose'")]
    [TestCase("""{ "opens": "later" }""", "opens: must be one of 'onArrival', 'manual'")]
    public async Task UpdatePoll_WhenASettingBreaksARule_ShouldRefuseNamingItAndChangeNothing(string settingsJson, string expected)
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settingsJson)!;
        arguments["deck"] = JsonSerializer.SerializeToElement(poll.DeckId);
        arguments["slide"] = JsonSerializer.SerializeToElement(poll.Slide);

        var error = await SlideCalls.ErrorAsync(agent, "update_poll", arguments);

        error.ShouldBe(expected);
        (await SettingsAsync(poll)).ShouldBe(PollSettings.Default);
        Publisher.PublishedFor(poll.DeckId).ShouldBeEmpty();
    }

    // One wire case per rule of the arguments: missing, null, mistyped and unknown, for each argument that has one.
    [TestCase("""{ "slide": 2 }""", "deck: required")]
    [TestCase("""{ "deck": null, "slide": 2 }""", "deck: required")]
    [TestCase("""{ "deck": 7, "slide": 2 }""", "deck: must be text")]
    [TestCase("""{ "deck": "d" }""", "slide: required")]
    [TestCase("""{ "deck": "d", "slide": null }""", "slide: required")]
    [TestCase("""{ "deck": "d", "slide": "2" }""", "slide: must be a whole number")]
    [TestCase("""{ "deck": "d", "slide": 2, "pick": 1 }""", "pick: must be text")]
    [TestCase("""{ "deck": "d", "slide": 2, "max": "2" }""", "max: must be a whole number")]
    [TestCase("""{ "deck": "d", "slide": 2, "results": false }""", "results: must be text")]
    [TestCase("""{ "deck": "d", "slide": 2, "correct": 1.5 }""", "correct: must be a whole number")]
    [TestCase("""{ "deck": "d", "slide": 2, "opens": [] }""", "opens: must be text")]
    [TestCase("""{ "deck": "d", "slide": 2, "privacy": "named" }""", "privacy: unknown argument")]
    public async Task UpdatePoll_WhenAnArgumentIsMissingNullMistypedOrUnknown_ShouldRefuseNamingIt(string argumentsJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "update_poll", argumentsJson);

        result.GetProperty("isError").GetBoolean().ShouldBeTrue();
        McpWire.TextOf(result).ShouldBe(expected);
    }

    [Test]
    public async Task UpdatePoll_WhenSlideIsNotAPollOrNotInTheDeck_ShouldRefuseNamingSlide()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        (await SlideCalls.ErrorAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.OtherSlide, results = "live" }))
            .ShouldBe("slide: slide 1 is not a poll; add one with add_slide and the poll layout");
        (await SlideCalls.ErrorAsync(agent, "update_poll", new { deck = poll.DeckId, slide = 9, results = "live" })).ShouldBe("slide: must be between 1 and 2");
    }

    [TestCase("update_poll")]
    [TestCase("add_poll_option")]
    [TestCase("open_poll")]
    [TestCase("close_poll")]
    [TestCase("get_poll_results")]
    public async Task PollTools_WhenCallerCannotEditTheDeck_ShouldRefuseAsNotFoundAndChangeNothing(string tool)
    {
        var poll = await SeedPollAsync();
        await SharingApi.PutOkAsync(Client, poll.DeckId, "link");
        await using var alex = await McpTestClient.ConnectAsAsync(Factory, TestUser.Alex);

        var arguments = tool switch
        {
            "update_poll" => new Dictionary<string, object> { ["deck"] = poll.DeckId, ["slide"] = poll.Slide, ["results"] = "afterClose" },
            "add_poll_option" => new Dictionary<string, object> { ["deck"] = poll.DeckId, ["slide"] = poll.Slide, ["text"] = "A fourth" },
            _ => new Dictionary<string, object> { ["deck"] = poll.DeckId, ["slide"] = poll.Slide },
        };

        var error = await SlideCalls.ErrorAsync(alex, tool, arguments);

        error.ShouldBe("Deck not found");
        (await SettingsAsync(poll)).ShouldBe(PollSettings.Default);
        Polls.CallsFor(poll.DeckId).ShouldBeEmpty();
        Publisher.PublishedFor(poll.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task AddPollOption_WhenNoRoundIsOpen_ShouldAppendAndReturnTheOptions()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "add_poll_option", new { deck = poll.DeckId, slide = poll.Slide, text = "Over 1,000 lines" });

        result.GetProperty("options").EnumerateArray().Select(o => o.GetString()).ShouldBe([.. PollSeed.OPTIONS, "Over 1,000 lines"]);
        var slide = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1];
        slide.GetProperty("slots").GetProperty("options").EnumerateArray().Select(o => o.GetString()).ShouldBe([.. PollSeed.OPTIONS, "Over 1,000 lines"]);
        var entry = (await DeckSeed.ActivityAsync(Client, poll.DeckId))[0];
        entry.GetProperty("tool").GetString().ShouldBe("add_poll_option");
        entry.GetProperty("summary").GetString().ShouldBe($"added option 4 to the poll on slide {poll.Slide}");
    }

    [Test]
    public async Task AddPollOption_WhenRoundIsOpen_ShouldRefreshAndPublish()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "add_poll_option", new { deck = poll.DeckId, slide = poll.Slide, text = "Over 1,000 lines" });

        result.GetProperty("options").GetArrayLength().ShouldBe(4);
        Relay.Refreshed.Where(deck => deck == poll.DeckId).ShouldBe([poll.DeckId]);
        Publisher.PublishedFor(poll.DeckId).ShouldBe([poll.Slide]);
    }

    [Test]
    public async Task AddPollOption_WhenAtEightOptions_ShouldRefuse()
    {
        var poll = await SeedPollAsync(options: ["1", "2", "3", "4", "5", "6", "7", "8"]);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var revision = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1].GetProperty("revision").GetInt64();

        var error = await SlideCalls.ErrorAsync(agent, "add_poll_option", new { deck = poll.DeckId, slide = poll.Slide, text = "9" });

        error.ShouldStartWith("poll.options");
        var slide = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1];
        slide.GetProperty("slots").GetProperty("options").GetArrayLength().ShouldBe(8);
        slide.GetProperty("revision").GetInt64().ShouldBe(revision);
        Relay.Refreshed.ShouldNotContain(poll.DeckId);
        Publisher.PublishedFor(poll.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task AddPollOption_WhenTextIsTooLongOrBlank_ShouldRefuseNamingTheOption()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        (await SlideCalls.ErrorAsync(agent, "add_poll_option", new { deck = poll.DeckId, slide = poll.Slide, text = new string('x', 61) })).ShouldStartWith("poll.options[3]");
        (await SlideCalls.ErrorAsync(agent, "add_poll_option", new { deck = poll.DeckId, slide = poll.Slide, text = "" })).ShouldStartWith("poll.options[3]");
    }

    [TestCase("""{ "slide": 2, "text": "x" }""", "deck: required")]
    [TestCase("""{ "deck": "d", "text": "x" }""", "slide: required")]
    [TestCase("""{ "deck": "d", "slide": 2 }""", "text: required")]
    [TestCase("""{ "deck": "d", "slide": 2, "text": null }""", "text: required")]
    [TestCase("""{ "deck": "d", "slide": 2, "text": 5 }""", "text: must be text")]
    [TestCase("""{ "deck": "d", "slide": "2", "text": "x" }""", "slide: must be a whole number")]
    [TestCase("""{ "deck": "d", "slide": 2, "text": "x", "position": 1 }""", "position: unknown argument")]
    public async Task AddPollOption_WhenAnArgumentIsMissingNullMistypedOrUnknown_ShouldRefuseNamingIt(string argumentsJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "add_poll_option", argumentsJson);

        result.GetProperty("isError").GetBoolean().ShouldBeTrue();
        McpWire.TextOf(result).ShouldBe(expected);
    }

    [Test]
    public async Task UpdateSlide_WhenRoundIsOpenAndOptionsShrink_ShouldRefuse()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var before = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1];
        var entries = (await DeckSeed.ActivityAsync(Client, poll.DeckId)).Count;

        var error = await SlideCalls.UpdateErrorAsync(agent, poll.DeckId, poll.Slide, before.GetProperty("revision").GetInt64(), """{ "question": "How big?", "options": ["Small", "Big"] }""");

        error.ShouldBe("slots.options: a round is open, so the options can't shrink from 3 to 2; close the poll first");
        var after = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1];
        after.GetProperty("revision").GetInt64().ShouldBe(before.GetProperty("revision").GetInt64());
        after.GetProperty("slots").GetRawText().ShouldBe(before.GetProperty("slots").GetRawText());
        (await DeckSeed.ActivityAsync(Client, poll.DeckId)).Count.ShouldBe(entries);
        Relay.Refreshed.ShouldNotContain(poll.DeckId);
        Publisher.PublishedFor(poll.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateSlide_WhenRoundIsOpenAndOptionsChangeOrder_ShouldRefuse()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var revision = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1].GetProperty("revision").GetInt64();

        var error = await SlideCalls.UpdateErrorAsync(agent, poll.DeckId, poll.Slide, revision, """{ "question": "How big?", "options": ["50 to 200 lines", "Under 50 lines", "Over 200 lines"] }""");

        error.ShouldBe("slots.options: a round is open, so the options can't change order; close the poll first");
        Publisher.PublishedFor(poll.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateSlide_WhenRoundIsOpenAndLayoutLeavesPoll_ShouldRefuse()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var revision = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1].GetProperty("revision").GetInt64();

        var error = await SlideCalls.UpdateErrorAsync(agent, poll.DeckId, poll.Slide, revision, """{ "quote": "Ship it", "attribution": "Sam" }""", layout: "quote");

        error.ShouldBe("A round of this poll is open, so the slide can't stop being a poll; close the poll first");
    }

    [Test]
    public async Task UpdateSlide_WhenRoundIsOpenAndOnlyTextIsFixedOrAppended_ShouldPassAndRefreshAndPublish()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var revision = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1].GetProperty("revision").GetInt64();

        var fixedText = await SlideCalls.UpdateAsync(agent, poll.DeckId, poll.Slide, revision, """{ "question": "How big was your last merge request?", "options": ["Under 50 lines", "50 to 200 lines", "Over 200 lines!"] }""");
        var appended = await SlideCalls.UpdateAsync(agent, poll.DeckId, poll.Slide, fixedText.GetProperty("revision").GetInt64(), """{ "question": "How big was your last merge request?", "options": ["Under 50 lines", "50 to 200 lines", "Over 200 lines!", "Huge"] }""");

        appended.GetProperty("revision").GetInt64().ShouldBeGreaterThan(fixedText.GetProperty("revision").GetInt64());
        Relay.Refreshed.Where(deck => deck == poll.DeckId).ShouldBe([poll.DeckId, poll.DeckId]);
        Publisher.PublishedFor(poll.DeckId).ShouldBe([poll.Slide, poll.Slide]);
    }

    [Test]
    public async Task UpdateSlide_WhenNothingAboutThePollChanged_ShouldNotRefreshOrPublish()
    {
        var poll = await SeedPollAsync();
        await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var slide = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1];

        await SlideCalls.UpdateAsync(agent, poll.DeckId, poll.Slide, slide.GetProperty("revision").GetInt64(), slide.GetProperty("slots").GetRawText());

        Relay.Refreshed.ShouldNotContain(poll.DeckId);
        Publisher.PublishedFor(poll.DeckId).ShouldBeEmpty();
    }

    [Test]
    public async Task UpdateSlide_WhenNoRoundIsOpenAndOptionsShrinkOrReorder_ShouldPass()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var revision = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1].GetProperty("revision").GetInt64();

        var reordered = await SlideCalls.UpdateAsync(agent, poll.DeckId, poll.Slide, revision, """{ "question": "How big?", "options": ["Over 200 lines", "Under 50 lines", "50 to 200 lines"] }""");
        var shrunk = await SlideCalls.UpdateAsync(agent, poll.DeckId, poll.Slide, reordered.GetProperty("revision").GetInt64(), """{ "question": "How big?", "options": ["Small", "Big"] }""");

        shrunk.GetProperty("revision").GetInt64().ShouldBeGreaterThan(reordered.GetProperty("revision").GetInt64());
    }

    [Test]
    public async Task UpdateSlide_WhenOptionsWouldLeaveCorrectOrMaxOutOfRange_ShouldRefuseNamingTheSetting()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await SlideCalls.OkAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, pick = "upTo", max = 3, correct = 3 });
        var revision = (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1].GetProperty("revision").GetInt64();

        var error = await SlideCalls.UpdateErrorAsync(agent, poll.DeckId, poll.Slide, revision, """{ "question": "How big?", "options": ["Small", "Big"] }""");

        error.ShouldBe("options: max: must be 2 to 2; change the poll's settings with update_poll first");
        (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1].GetProperty("slots").GetProperty("options").GetArrayLength().ShouldBe(3);
    }

    [Test]
    public async Task OpenPoll_WhenAgentHasEditAccess_ShouldCallOpenAndReturnTheControlView()
    {
        var poll = await SeedPollAsync();
        Polls.Set(poll.DeckId, poll.Slide, ControlView(poll, PollPhase.Open));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "open_poll", new { deck = poll.DeckId, slide = poll.Slide });

        Polls.CallsFor(poll.DeckId).ShouldBe([$"Open {poll.Slide}"]);
        result.GetProperty("phase").GetString().ShouldBe("open");
        result.GetProperty("slide").GetInt32().ShouldBe(poll.Slide);
    }

    [Test]
    public async Task ClosePoll_WhenAgentHasEditAccess_ShouldCallCloseAndReturnTheControlView()
    {
        var poll = await SeedPollAsync();
        Polls.Set(poll.DeckId, poll.Slide, ControlView(poll, PollPhase.Closed));
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "close_poll", new { deck = poll.DeckId, slide = poll.Slide });

        Polls.CallsFor(poll.DeckId).ShouldBe([$"Close {poll.Slide}"]);
        result.GetProperty("phase").GetString().ShouldBe("closed");
    }

    [TestCase("open_poll")]
    [TestCase("close_poll")]
    public async Task PollControlTools_WhenDeckIsNotLive_ShouldRefuseWithNotLiveAndCallNothing(string tool)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var slide = (await DeckSeed.AddSlideAsync(agent, deck, "poll", FOUR_OPTIONS)).GetProperty("slide").GetInt32();

        var error = await SlideCalls.ErrorAsync(agent, tool, new { deck, slide });

        error.ShouldStartWith("not_live: ");
        Polls.CallsFor(deck).ShouldBeEmpty();
    }

    [TestCase("open_poll", PollRefusals.WRONG_PHASE, "wrong_phase: ")]
    [TestCase("close_poll", PollRefusals.WRONG_PHASE, "wrong_phase: ")]
    [TestCase("open_poll", PollRefusals.ANOTHER_POLL_OPEN, "another_poll_open: ")]
    public async Task PollControlTools_WhenTheLifecycleRefuses_ShouldGiveItsCodeAndASentence(string tool, string code, string expectedStart)
    {
        var poll = await SeedPollAsync();
        Polls.RefuseWith(code);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var error = await SlideCalls.ErrorAsync(agent, tool, new { deck = poll.DeckId, slide = poll.Slide });

        error.ShouldStartWith(expectedStart);
        Polls.CallsFor(poll.DeckId).ShouldBeEmpty();
    }

    [TestCase("""{ "slide": 2 }""", "deck: required")]
    [TestCase("""{ "deck": "d" }""", "slide: required")]
    [TestCase("""{ "deck": "d", "slide": null }""", "slide: required")]
    [TestCase("""{ "deck": "d", "slide": "2" }""", "slide: must be a whole number")]
    [TestCase("""{ "deck": "d", "slide": 2, "round": 1 }""", "round: unknown argument")]
    public async Task PollControlTools_WhenAnArgumentIsMissingNullMistypedOrUnknown_ShouldRefuseNamingIt(string argumentsJson, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        foreach (var tool in new[] { "open_poll", "close_poll", "get_poll_results" })
        {
            var result = await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, tool, argumentsJson);

            result.GetProperty("isError").GetBoolean().ShouldBeTrue(tool);
            McpWire.TextOf(result).ShouldBe(expected, tool);
        }
    }

    [Test]
    public async Task GetPollResults_WhenTwoRoundsClosed_ShouldListBothWithRates()
    {
        var poll = await SeedPollAsync();
        var first = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(first.Id, "results-guest-1", 0);
        await Rows.AnswerAsync(first.Id, "results-guest-2", 1);
        await Rows.AnswerAsync(first.Id, "results-guest-3", 1);
        await Rows.CloseAsync(first.Id, joinedAtClose: 4);
        var second = await Rows.RoundAsync(poll.SlideId, poll.SessionId, number: 2);
        await Rows.AnswerAsync(second.Id, "results-guest-1", 2);
        await Rows.AnswerAsync(second.Id, "results-guest-4", 2);
        await Rows.CloseAsync(second.Id, joinedAtClose: 3);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "get_poll_results", new { deck = poll.DeckId, slide = poll.Slide });

        result.GetProperty("question").GetString().ShouldBe(PollSeed.QUESTION);
        result.GetProperty("options").EnumerateArray().Select(o => o.GetString()).ShouldBe(PollSeed.OPTIONS);
        var session = result.GetProperty("sessions").EnumerateArray().Single();
        var rounds = session.GetProperty("rounds").EnumerateArray().ToList();
        rounds.Select(r => r.GetProperty("number").GetInt32()).ShouldBe([1, 2]);
        rounds.Select(r => r.GetProperty("answered").GetInt32()).ShouldBe([3, 2]);
        rounds.Select(r => r.GetProperty("joined").GetInt32()).ShouldBe([4, 3]);
        rounds.Select(r => r.GetProperty("rate").GetDouble()).ShouldBe([0.75, 2d / 3]);
        rounds[0].GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([1, 2, 0]);
        rounds[1].GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([0, 0, 2]);
        session.GetProperty("startedAt").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task GetPollResults_WhenRoundsAreOpenOrDraftOrFromEarlierSessions_ShouldListOnlyOpenedAndClosedOnesPerSession()
    {
        var poll = await SeedPollAsync();
        var earlier = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(earlier.Id, "session-one-guest", 1);
        await Rows.CloseAsync(earlier.Id, joinedAtClose: 2);
        Clock.Advance(TimeSpan.FromMinutes(10));
        (await Client.DeleteAsync($"/api/decks/{poll.DeckId}/live")).EnsureSuccessStatusCode();
        var live = await (await Client.PostAsync($"/api/decks/{poll.DeckId}/live", null)).Content.ReadFromJsonAsync<JsonElement>();
        var secondSession = Guid.Parse(live.GetProperty("sessionId").GetString()!);
        var later = await Rows.RoundAsync(poll.SlideId, secondSession);
        await Rows.CloseAsync(later.Id, joinedAtClose: 5);
        await Rows.RoundAsync(poll.SlideId, secondSession, number: 2);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "get_poll_results", new { deck = poll.DeckId, slide = poll.Slide });

        var sessions = result.GetProperty("sessions").EnumerateArray().ToList();
        sessions.Count.ShouldBe(2);
        sessions.Select(s => s.GetProperty("rounds").GetArrayLength()).ShouldBe([1, 1]);
        sessions[0].GetProperty("rounds")[0].GetProperty("joined").GetInt32().ShouldBe(2);
        sessions[1].GetProperty("rounds")[0].GetProperty("joined").GetInt32().ShouldBe(5);
        sessions[1].GetProperty("rounds")[0].GetProperty("rate").GetDouble().ShouldBe(0);
    }

    [Test]
    public async Task GetPollResults_WhenAnOptionWasAddedAfterARoundClosed_ShouldPadItsCountsToTheOptions()
    {
        var poll = await SeedPollAsync();
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "padded-guest", 2);
        await Rows.CloseAsync(round.Id, joinedAtClose: 1);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await SlideCalls.OkAsync(agent, "add_poll_option", new { deck = poll.DeckId, slide = poll.Slide, text = "Huge" });

        var result = await SlideCalls.OkAsync(agent, "get_poll_results", new { deck = poll.DeckId, slide = poll.Slide });

        result.GetProperty("options").GetArrayLength().ShouldBe(4);
        result.GetProperty("sessions")[0].GetProperty("rounds")[0].GetProperty("counts").EnumerateArray().Select(c => c.GetInt32()).ShouldBe([0, 0, 1, 0]);
    }

    [Test]
    public async Task GetPollResults_WhenCalled_ShouldNeverContainGuestIds()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await SlideCalls.OkAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, correct = 2 });
        var round = await Rows.RoundAsync(poll.SlideId, poll.SessionId);
        await Rows.AnswerAsync(round.Id, "guest-aaaa1111", 1);
        await Rows.AnswerAsync(round.Id, "guest-bbbb2222", 2);
        await Rows.CloseAsync(round.Id, joinedAtClose: 2, answerShown: true);

        var (isError, _) = await agent.CallToolAsync("get_poll_results", new { deck = poll.DeckId, slide = poll.Slide });
        var raw = McpWire.TextOf(await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "get_poll_results", JsonSerializer.Serialize(new { deck = poll.DeckId, slide = poll.Slide })));

        isError.ShouldBeFalse();
        raw.ShouldNotContain("guest-aaaa1111");
        raw.ShouldNotContain("guest-bbbb2222");
        raw.ShouldNotContain("guest");
        raw.ShouldNotContain("correct");
        raw.ShouldNotContain("picks");
        JsonSerializer.Deserialize<JsonElement>(raw).EnumerateObject().Select(p => p.Name).ShouldBe(["question", "options", "sessions"]);
    }

    [Test]
    public async Task GetPollResults_WhenSlideIsNotAPollOrNotInTheDeck_ShouldRefuseNamingSlide()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        (await SlideCalls.ErrorAsync(agent, "get_poll_results", new { deck = poll.DeckId, slide = poll.OtherSlide })).ShouldBe("slide: slide 1 is not a poll");
        (await SlideCalls.ErrorAsync(agent, "get_poll_results", new { deck = poll.DeckId, slide = 0 })).ShouldBe("slide: must be between 1 and 2");
    }

    [Test]
    public async Task GetPollResults_WhenNoRoundWasEverOpened_ShouldListNoSessions()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var result = await SlideCalls.OkAsync(agent, "get_poll_results", new { deck = poll.DeckId, slide = poll.Slide });

        result.GetProperty("sessions").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task GetDeck_WhenSlideIsAPoll_ShouldNotContainCorrect()
    {
        var poll = await SeedPollAsync();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await SlideCalls.OkAsync(agent, "update_poll", new { deck = poll.DeckId, slide = poll.Slide, pick = "upTo", max = 2, correct = 2, results = "afterClose", opens = "manual" });

        var wire = McpWire.TextOf(await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "get_deck", JsonSerializer.Serialize(new { deck = poll.DeckId })));
        var page = await Client.GetStringAsync($"/api/decks/{poll.DeckId}");

        wire.ShouldNotContain("correct");
        wire.ShouldNotContain("afterClose");
        wire.ShouldNotContain("manual");
        page.ShouldNotContain("correct");
        page.ShouldNotContain("afterClose");
        (await SlideCalls.SlidesAsync(agent, poll.DeckId))[poll.Slide - 1].GetProperty("slots").EnumerateObject().Select(p => p.Name).ShouldBe(["question", "options"]);
    }

    [Test]
    public async Task DeckPage_WhenSlideIsAPoll_ShouldUseTheQuestionAsItsHeadline()
    {
        var poll = await SeedPollAsync();

        var page = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{poll.DeckId}");

        page.GetProperty("slides")[poll.Slide - 1].GetProperty("headline").GetString().ShouldBe(PollSeed.QUESTION);
    }

    // A poll and a join slide are for the live room, not for a kind of talk, so no template warns about them.
    [Test]
    public async Task AddSlide_WhenPollOrJoinGoesInADeckOfAnyTemplate_ShouldRaiseNoTemplateWarning()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        foreach (var template in new TemplateCatalogue().All)
        {
            var deck = (await SlideCalls.OkAsync(agent, "create_deck", new { title = "Ship smaller", template = template.Name })).GetProperty("deck").GetString()!;

            var poll = await DeckSeed.AddSlideAsync(agent, deck, "poll", FOUR_OPTIONS);
            var join = await DeckSeed.AddSlideAsync(agent, deck, "join", "{}");
            var pollRevision = poll.GetProperty("revision").GetInt64();
            var updated = await SlideCalls.UpdateAsync(agent, deck, 1, pollRevision, FOUR_OPTIONS);

            poll.GetProperty("warnings").GetArrayLength().ShouldBe(0, template.Name);
            join.GetProperty("warnings").GetArrayLength().ShouldBe(0, template.Name);
            updated.GetProperty("warnings").GetArrayLength().ShouldBe(0, template.Name);
        }
    }

    private async Task<PollSettings> SettingsAsync(SeededPoll poll)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<PollRows>().SettingsAsync(poll.SlideId))!;
    }

    private static PollControlView ControlView(SeededPoll poll, PollPhase phase) => new(
        1,
        poll.Slide,
        phase == PollPhase.Draft ? null : Guid.NewGuid().ToString(),
        1,
        phase,
        PollSettings.Default,
        PollSeed.QUESTION,
        PollSeed.OPTIONS,
        0,
        0,
        [0, 0, 0],
        true,
        false,
        false,
        []);
}
