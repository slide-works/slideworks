using System.Text.Json;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Slides;

// A poll slide added with its settings is finished when add_slide or add_slides returns. The settings are not slots.
[TestFixture]
[CancelAfter(30000)]
public class AddPollSettingsTests : ApiTestFixture
{
    private const string QUESTION = "Which part should we cut?";

    [Test]
    public async Task AddSlide_WhenPollSettingsAreSet_ShouldStoreThem()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var added = await SlideCalls.OkAsync(agent, "add_slide", new
        {
            deck,
            layout = "poll",
            slots = PollSlots(),
            poll = new { pick = "upTo", max = 2, correct = 3, results = "afterClose", opens = "manual" },
        });

        added.GetProperty("slide").GetInt32().ShouldBe(1);
        var poll = added.GetProperty("poll");
        poll.GetProperty("pick").GetString().ShouldBe("upTo");
        poll.GetProperty("max").GetInt32().ShouldBe(2);
        poll.GetProperty("correct").GetInt32().ShouldBe(3);
        poll.GetProperty("results").GetString().ShouldBe("afterClose");
        poll.GetProperty("opens").GetString().ShouldBe("manual");
        poll.GetProperty("privacy").GetString().ShouldBe("private");
        var stored = (await SlideCalls.SlidesAsync(agent, deck))[0];
        stored.GetProperty("layout").GetString().ShouldBe("poll");
        stored.GetProperty("slots").GetProperty("question").GetString().ShouldBe(QUESTION);
        stored.GetProperty("slots").EnumerateObject().Select(p => p.Name).ShouldBe(["question", "options"]);
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("tool").GetString().ShouldBe("add_slide");
    }

    [Test]
    public async Task AddSlide_WhenPollSettingsAreOmitted_ShouldReturnTheDefaults()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var added = await DeckSeed.AddSlideAsync(agent, deck, "poll", """{ "question": "Which part should we cut?", "options": ["The intro", "The demo", "The recap"] }""");

        added.EnumerateObject().Select(p => p.Name).ShouldBe(["slide", "steps", "revision", "url", "warnings", "poll"], ignoreOrder: true);
        var poll = added.GetProperty("poll");
        poll.GetProperty("pick").GetString().ShouldBe("one");
        poll.GetProperty("max").ValueKind.ShouldBe(JsonValueKind.Null);
        poll.GetProperty("results").GetString().ShouldBe("live");
        poll.GetProperty("correct").ValueKind.ShouldBe(JsonValueKind.Null);
        poll.GetProperty("opens").GetString().ShouldBe("onArrival");
        poll.GetProperty("privacy").GetString().ShouldBe("private");
    }

    [Test]
    public async Task AddSlide_WhenPollSettingsAreRejected_ShouldStoreNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Keep");

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new
        {
            deck,
            layout = "poll",
            slots = PollSlots(),
            poll = new { max = 2 },
        });

        error.ShouldBe("max: only when pick is upTo");
        var again = await DeckSeed.AddQuoteAsync(agent, deck, "Still");
        again.GetProperty("slide").GetInt32().ShouldBe(2);
        again.GetProperty("revision").GetInt64().ShouldBe(2);
        var stored = await SlideCalls.SlidesAsync(agent, deck);
        stored.Count.ShouldBe(2);
        stored[0].GetProperty("revision").GetInt64().ShouldBe(1);
        stored[0].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Keep");
        stored[1].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Still");
    }

    [TestCase("max", "2", "max: must be a whole number")]
    [TestCase("correct", "1", "correct: must be a whole number")]
    public async Task AddSlide_WhenACountIsNotANumber_ShouldStoreNothing(string field, string text, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Keep");

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new
        {
            deck,
            layout = "poll",
            slots = PollSlots(),
            poll = new Dictionary<string, object> { [field] = text },
        });

        error.ShouldBe(expected);
        var again = await DeckSeed.AddQuoteAsync(agent, deck, "Still");
        again.GetProperty("slide").GetInt32().ShouldBe(2);
        again.GetProperty("revision").GetInt64().ShouldBe(2);
        var stored = await SlideCalls.SlidesAsync(agent, deck);
        stored.Count.ShouldBe(2);
        stored[0].GetProperty("revision").GetInt64().ShouldBe(1);
        stored[0].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Keep");
        stored[1].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Still");
    }

    [Test]
    public async Task AddSlide_WhenAQuoteCarriesPoll_ShouldStoreNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Keep");

        var error = await SlideCalls.ErrorAsync(agent, "add_slide", new
        {
            deck,
            layout = "quote",
            slots = new { quote = "Ship it.", attribution = "Sam" },
            poll = new { results = "live" },
        });

        error.ShouldBe("poll: only the poll layout takes settings");
        var stored = await SlideCalls.SlidesAsync(agent, deck);
        stored.Count.ShouldBe(1);
        stored[0].GetProperty("revision").GetInt64().ShouldBe(1);
        stored[0].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Keep");
    }

    [Test]
    public async Task AddSlides_WhenAPollEntryCarriesSettings_ShouldStoreThoseSettings()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var added = await SlideCalls.OkAsync(agent, "add_slides", new
        {
            deck,
            slides = new object[]
            {
                new { layout = "quote", slots = new { quote = "Yes", attribution = "Sam" } },
                new
                {
                    layout = "poll",
                    slots = PollSlots(),
                    poll = new { pick = "upTo", max = 2, correct = 1, results = "afterAnswer", opens = "manual" },
                },
            },
        });

        added.GetProperty("added").GetInt32().ShouldBe(2);
        var slides = added.GetProperty("slides").EnumerateArray().ToList();
        slides[0].GetProperty("ok").GetBoolean().ShouldBeTrue();
        slides[0].TryGetProperty("poll", out _).ShouldBeFalse();
        slides[1].GetProperty("ok").GetBoolean().ShouldBeTrue();
        slides[1].GetProperty("slide").GetInt32().ShouldBe(2);
        var poll = slides[1].GetProperty("poll");
        poll.GetProperty("pick").GetString().ShouldBe("upTo");
        poll.GetProperty("max").GetInt32().ShouldBe(2);
        poll.GetProperty("correct").GetInt32().ShouldBe(1);
        poll.GetProperty("results").GetString().ShouldBe("afterAnswer");
        poll.GetProperty("opens").GetString().ShouldBe("manual");
        poll.GetProperty("privacy").GetString().ShouldBe("private");
    }

    [Test]
    public async Task AddSlides_WhenOnePollEntryIsRejected_ShouldLeaveThatSlideOutAndStoreTheOthers()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Keep");

        var added = await SlideCalls.OkAsync(agent, "add_slides", new
        {
            deck,
            slides = new object[]
            {
                new { layout = "quote", slots = new { quote = "Yes", attribution = "Sam" } },
                new { layout = "poll", slots = PollSlots(), poll = new { max = 2 } },
                new { layout = "quote", slots = new { quote = "Also", attribution = "Sam" } },
            },
        });

        added.GetProperty("added").GetInt32().ShouldBe(2);
        var slides = added.GetProperty("slides").EnumerateArray().ToList();
        slides[0].GetProperty("ok").GetBoolean().ShouldBeTrue();
        slides[0].GetProperty("slide").GetInt32().ShouldBe(2);
        slides[1].GetProperty("ok").GetBoolean().ShouldBeFalse();
        slides[1].GetProperty("error").GetString().ShouldBe("max: only when pick is upTo");
        slides[1].GetProperty("slide").ValueKind.ShouldBe(JsonValueKind.Null);
        slides[1].TryGetProperty("poll", out _).ShouldBeFalse();
        slides[2].GetProperty("ok").GetBoolean().ShouldBeTrue();
        slides[2].GetProperty("slide").GetInt32().ShouldBe(3);
        var stored = await SlideCalls.SlidesAsync(agent, deck);
        stored.Count.ShouldBe(3);
        stored[0].GetProperty("revision").GetInt64().ShouldBe(1);
        stored[0].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Keep");
        stored[1].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Yes");
        stored[1].GetProperty("revision").GetInt64().ShouldBe(2);
        stored[2].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Also");
        stored[2].GetProperty("revision").GetInt64().ShouldBe(2);
    }

    [TestCase("max", "2", "max: must be a whole number")]
    [TestCase("correct", "1", "correct: must be a whole number")]
    public async Task AddSlides_WhenACountIsNotANumber_ShouldLeaveThatSlideOutAndStoreTheOthers(string field, string text, string expected)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Keep");

        var added = await SlideCalls.OkAsync(agent, "add_slides", new
        {
            deck,
            slides = new object[]
            {
                new { layout = "quote", slots = new { quote = "Yes", attribution = "Sam" } },
                new { layout = "poll", slots = PollSlots(), poll = new Dictionary<string, object> { [field] = text } },
                new { layout = "quote", slots = new { quote = "Also", attribution = "Sam" } },
            },
        });

        added.GetProperty("added").GetInt32().ShouldBe(2);
        var slides = added.GetProperty("slides").EnumerateArray().ToList();
        slides[0].GetProperty("ok").GetBoolean().ShouldBeTrue();
        slides[0].GetProperty("slide").GetInt32().ShouldBe(2);
        slides[1].GetProperty("ok").GetBoolean().ShouldBeFalse();
        slides[1].GetProperty("error").GetString().ShouldBe(expected);
        slides[1].GetProperty("slide").ValueKind.ShouldBe(JsonValueKind.Null);
        slides[2].GetProperty("ok").GetBoolean().ShouldBeTrue();
        slides[2].GetProperty("slide").GetInt32().ShouldBe(3);
        var stored = await SlideCalls.SlidesAsync(agent, deck);
        stored.Count.ShouldBe(3);
        stored[0].GetProperty("revision").GetInt64().ShouldBe(1);
        stored[0].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Keep");
        stored[1].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Yes");
        stored[2].GetProperty("slots").GetProperty("quote").GetString().ShouldBe("Also");
    }

    [Test]
    public async Task AddSlides_WhenAPollEntryIsMalformed_ShouldLeaveThatSlideOutAndStoreTheOthers()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var added = await SlideCalls.OkAsync(agent, "add_slides", new
        {
            deck,
            slides = new object[]
            {
                new { layout = "quote", slots = new { quote = "Yes", attribution = "Sam" } },
                new { layout = "poll", slots = PollSlots(), poll = "nope" },
                new { layout = "quote", slots = new { quote = "Also", attribution = "Sam" } },
            },
        });

        added.GetProperty("added").GetInt32().ShouldBe(2);
        var slides = added.GetProperty("slides").EnumerateArray().ToList();
        slides[0].GetProperty("ok").GetBoolean().ShouldBeTrue();
        slides[1].GetProperty("ok").GetBoolean().ShouldBeFalse();
        slides[1].GetProperty("error").GetString().ShouldBe("poll: must be an object");
        slides[2].GetProperty("ok").GetBoolean().ShouldBeTrue();
        slides[2].GetProperty("slide").GetInt32().ShouldBe(2);
        var stored = await SlideCalls.SlidesAsync(agent, deck);
        stored.Count.ShouldBe(2);
        stored.Select(s => s.GetProperty("slots").GetProperty("quote").GetString()).ShouldBe(["Yes", "Also"]);
    }

    [Test]
    public async Task AddSlides_WhenAQuoteEntryCarriesPoll_ShouldLeaveThatSlideOutAndStoreTheOthers()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var added = await SlideCalls.OkAsync(agent, "add_slides", new
        {
            deck,
            slides = new object[]
            {
                new { layout = "quote", slots = new { quote = "Yes", attribution = "Sam" }, poll = new { results = "live" } },
                new { layout = "poll", slots = PollSlots() },
            },
        });

        added.GetProperty("added").GetInt32().ShouldBe(1);
        var slides = added.GetProperty("slides").EnumerateArray().ToList();
        slides[0].GetProperty("ok").GetBoolean().ShouldBeFalse();
        slides[0].GetProperty("error").GetString().ShouldBe("poll: only the poll layout takes settings");
        slides[1].GetProperty("ok").GetBoolean().ShouldBeTrue();
        slides[1].GetProperty("slide").GetInt32().ShouldBe(1);
        slides[1].GetProperty("poll").GetProperty("pick").GetString().ShouldBe("one");
        var stored = await SlideCalls.SlidesAsync(agent, deck);
        stored.Count.ShouldBe(1);
        stored[0].GetProperty("layout").GetString().ShouldBe("poll");
    }

    private static object PollSlots() => new { question = QUESTION, options = new[] { "The intro", "The demo", "The recap" } };
}
