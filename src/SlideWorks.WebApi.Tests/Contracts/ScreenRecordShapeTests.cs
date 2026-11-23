using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Checks;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Accounts.Admin;
using SlideWorks.WebApi.Features.Accounts.Avatars;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Decks.Views;
using SlideWorks.WebApi.Features.Join.Views;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Sharing.Views;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Contracts;

// The K4 screen records against their JSON examples in src/contracts/screens. Each record is built with every field set
// from the example's own values and serialised with the app's JSON options, so the JSON must equal the example exactly:
// names, nesting, value kinds and the wire values ("agent", "mcp", "link"). JsonShape then checks the nullable paths,
// the same paths the endpoints' tests use on their real responses.
[TestFixture]
public class ScreenRecordShapeTests : ApiTestFixture
{
    [Test]
    public void DecksLibrary_ShouldMatchContract()
    {
        var example = Example("decks-library.json");
        var view = new DecksLibraryView(
            [
                new DeckRow("7Kq2xYp9LmQa", "ship-smaller", "Ship smaller", "Team offsite · draft", 16,
                    LastChange.From(Entry("Claude", ActorKind.Agent, ActorVia.Mcp, "add_slide", "added slide 16")),
                    DeckSharing.Link, 0, "2 min ago", "/d/7Kq2xYp9LmQa/ship-smaller"),
                new DeckRow("Q4pRv8sT2wXz", "q4-platform-review", "Q4 platform review", "Leadership update", 22,
                    LastChange.From(Entry("You", ActorKind.Person, ActorVia.Web, null, "changed sharing")),
                    DeckSharing.Named, 3, "1 h ago", "/d/Q4pRv8sT2wXz/q4-platform-review"),
            ],
            [
                SharedDeckRow.From(
                    new DeckRow("Ob3nWk1Jn9Ef", "onboarding-week-one", "Onboarding: week one", "For new joiners", 12,
                        LastChange.From(Entry("Cursor", ActorKind.Agent, ActorVia.Mcp, "set_notes", "updated notes")),
                        DeckSharing.Named, 5, "Yesterday", "/d/Ob3nWk1Jn9Ef/onboarding-week-one"),
                    "Alex Chen",
                    "read"),
            ],
            new ConnectAgent(Show: true, McpUrl: "https://slideworks.example/mcp"));

        var json = Serialise(view);

        JsonShape.AssertMatches(json, "decks-library.json", ScreenShapes.DecksLibrary);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void DecksLibrary_WhenOptionalFieldsAreNull_ShouldMatchContract()
    {
        var row = new DeckRow("7Kq2xYp9LmQa", "ship-smaller", "Ship smaller", null, 0, null, DeckSharing.Private, 0, "just now", "/d/7Kq2xYp9LmQa/ship-smaller");
        var view = new DecksLibraryView([row], [SharedDeckRow.From(row, "Alex Chen", "read")], new ConnectAgent(false, "https://slideworks.example/mcp"));

        var json = Serialise(view);

        JsonShape.AssertMatches(json, "decks-library.json", ScreenShapes.DecksLibrary);
        json.GetProperty("sharedWithMe")[0].GetProperty("lastChange").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("decks")[0].GetProperty("sharing").GetString().ShouldBe("private");
    }

    [Test]
    public void DeckPage_ShouldMatchContract()
    {
        var example = Example("deck-page.json");
        var deck = new Deck { Id = "7Kq2xYp9LmQa", Slug = "ship-smaller", Title = "Ship smaller", PartsJson = "[]", Template = "workshop", Palette = "cobalt" };
        var slides = example.GetProperty("slides").EnumerateArray()
            .Select((slide, position) => DeckPageSlide.From(deck, new Slide
            {
                DeckId = deck.Id,
                Position = position,
                Layout = slide.GetProperty("layout").GetString()!,
                Part = slide.GetProperty("part").GetInt32(),
                SlotsJson = slide.GetProperty("slots").GetRawText(),
                Notes = StoredNotes(slide),
                Steps = slide.GetProperty("steps").GetInt32(),
                DeepDive = slide.GetProperty("deepDive").GetBoolean(),
            }, Workshop))
            .ToList();
        var view = new DeckPageView(
            deck.Id,
            deck.Slug,
            deck.Title,
            "Sam Rivera",
            example.GetProperty("revision").GetInt64(),
            IsOwner: true,
            CanEdit: true,
            Access: "owner",
            PlayUrl: "/d/7Kq2xYp9LmQa/ship-smaller",
            example.GetProperty("join").Deserialize<JoinView>(AppJsonOptions())!,
            SharingSummary.From(DeckSharing.Link, 0),
            [new DeckPart("The cost"), new DeckPart("Slice it"), new DeckPart("Ship it"), new DeckPart("Make it stick")],
            example.GetProperty("look").Deserialize<DeckLookView>(AppJsonOptions())!,
            slides,
            [
                ActivityRow.From(Entry("Claude", ActorKind.Agent, ActorVia.Mcp, "add_slide", "added slide 3 close"), "2 min ago"),
                ActivityRow.From(Entry("Claude", ActorKind.Agent, ActorVia.Mcp, "set_sharing", "changed sharing: Private → Anyone with the link"), "5 min ago"),
                ActivityRow.From(Entry("Claude", ActorKind.Agent, ActorVia.Mcp, "create_deck", "created the deck"), "9 min ago"),
            ],
            example.GetProperty("findings").Deserialize<IReadOnlyList<SlideFinding>>(AppJsonOptions())!);

        var json = Serialise(view);

        JsonShape.AssertMatches(json, "deck-page.json", ScreenShapes.DeckPage);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
    }

    [Test]
    public void DeckPage_WhenOptionalFieldsAreNull_ShouldMatchContract()
    {
        var deck = new Deck { Id = "7Kq2xYp9LmQa", Slug = "ship-smaller", Title = "Ship smaller", PartsJson = "[]", Template = "workshop", Palette = "cobalt" };
        var slide = DeckPageSlide.From(deck, new Slide { DeckId = deck.Id, Layout = "bullets", SlotsJson = """{ "items": ["One"] }""" }, Workshop);
        var view = new DeckPageView(deck.Id, deck.Slug, deck.Title, null, 0, false, false, "read", "/d/7Kq2xYp9LmQa/ship-smaller",
            new JoinView("482913", "482 913", "https://slideworks.live/482913", "slideworks.live", false), SharingSummary.From(DeckSharing.Private, 0), [], LookViews.ForPage(deck, Workshop, Palettes), [slide],
            [ActivityRow.From(Entry("Sam Rivera", ActorKind.Person, ActorVia.Web, null, "created the deck"), "just now")], []);

        var json = Serialise(view);

        JsonShape.AssertMatches(json, "deck-page.json", ScreenShapes.DeckPage);
        json.GetProperty("author").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("slides")[0].GetProperty("headline").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("activity")[0].GetProperty("actorKind").GetString().ShouldBe("person");
        json.GetProperty("activity")[0].GetProperty("via").GetString().ShouldBe("web");
        json.GetProperty("look").GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("look").GetProperty("chromeOverrides").GetProperty("tracker").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // The K4 headline rule: the headline slot, else title.title, divider.title, or the first 80 characters of quote.quote.
    [TestCase("bullets", """{ "headline": "Three things", "items": ["One"] }""", "Three things")]
    [TestCase("close", """{ "headline": "Pick one story.\nShip half of it." }""", "Pick one story.\nShip half of it.")]
    [TestCase("title", """{ "kicker": "Offsite", "title": "Ship smaller" }""", "Ship smaller")]
    [TestCase("divider", """{ "title": "Slice it", "sub": "Part two" }""", "Slice it")]
    [TestCase("quote", """{ "quote": "Small batches win.", "attribution": "Sam" }""", "Small batches win.")]
    public void DeckPageSlide_Headline_ShouldFollowTheLayoutsHeadlineRule(string layout, string slots, string expected)
    {
        Slide(layout, slots).Headline.ShouldBe(expected);
    }

    [Test]
    public void DeckPageSlide_WhenQuoteIsLong_ShouldTakeItsFirst80Characters()
    {
        var quote = new string('a', 79) + "bcdef";

        Slide("quote", $$"""{ "quote": "{{quote}}", "attribution": "Sam" }""").Headline.ShouldBe(new string('a', 79) + "b");
    }

    [Test]
    public void DeckPageSlide_WhenThe80thCharacterStartsAnEmoji_ShouldNotSplitIt()
    {
        var quote = new string('a', 79) + "😀 and more";

        Slide("quote", JsonSerializer.Serialize(new { quote, attribution = "Sam" })).Headline.ShouldBe(new string('a', 79));
    }

    [Test]
    public void DeckPageSlide_ShouldLinkRootRelativeToItsNumber()
    {
        var slide = Slide("divider", """{ "title": "Slice it" }""", position: 4);

        slide.Number.ShouldBe(5);
        slide.Link.ShouldBe("/d/7Kq2xYp9LmQa/ship-smaller#5");
    }

    [Test]
    public void Sharing_ShouldMatchContract()
    {
        var example = Example("sharing.json");
        var view = new SharingView(
            DeckSharing.Named,
            ["alex@team.example", "mira@team.example"],
            "https://slideworks.example/d/7Kq2xYp9LmQa/ship-smaller",
            SharingLastChange.From(Entry("Claude", ActorKind.Agent, ActorVia.Mcp, "set_sharing", "changed sharing"), "just now"),
            new SharingOwnerView("sam@team.example", "Sam Rivera", new AvatarView("SR", "teal", "https://slideworks.example/avatars/sam-rivera.png")),
            [
                new SharingPersonView("alex@team.example", "Alex Chen", new AvatarView("AC", "blue", "https://slideworks.example/avatars/alex-chen.png"), "write"),
                new SharingPersonView("mira@team.example", null, null, "read"),
            ]);

        var json = Serialise(view);

        JsonShape.AssertMatches(json, "sharing.json", ScreenShapes.Sharing);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
        JsonShape.AssertMatches(Serialise(view with { LastChange = null, Viewers = [], People = [] }), "sharing.json", ScreenShapes.Sharing);
    }

    // The PUT body is { level, viewers }, read with the app's JSON options.
    [Test]
    public void SharingUpdate_ShouldReadThePutBody()
    {
        var update = JsonSerializer.Deserialize<SharingUpdate>("""{ "level": "named", "viewers": ["alex@team.example"] }""", AppJsonOptions())!;

        update.Level.ShouldBe(DeckSharing.Named);
        update.Viewers.ShouldBe(["alex@team.example"]);
    }

    // An enum is read by its wire name only: a number, or a number in a string, is not a level.
    [TestCase("1")]
    [TestCase("\"1\"")]
    public void SharingUpdate_WhenLevelIsANumber_ShouldBeRefused(string level)
    {
        var body = $$"""{ "level": {{level}}, "viewers": [] }""";

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<SharingUpdate>(body, AppJsonOptions()));
    }

    // GET /api/me is real already: the shape test checks the response
    // for shape only, and the record with every field set.
    [Test]
    public async Task Me_ShouldMatchContract()
    {
        var response = await Client.GetFromJsonAsync<JsonElement>("/api/me");

        JsonShape.AssertMatches(response, "me.json", ScreenShapes.Me);
        var record = Serialise(new MeView("sam@team.example", "Sam Rivera", "https://slideworks.example/avatars/sam-rivera.png", new AvatarView("SR", "teal", "https://slideworks.example/avatars/sam-rivera.png"), "Europe/London", "approved", false, 0, false));
        JsonShape.AssertMatches(record, "me.json");
        SameJson(record, Example("me.json")).ShouldBeTrue(Differs(record, Example("me.json")));
    }

    [Test]
    public void AdminUsers_ShouldMatchContract()
    {
        var example = Example("admin-users.json");
        var users = example.GetProperty("users").EnumerateArray()
            .Select(row => new AdminUserRow(
                row.GetProperty("id").GetGuid(),
                row.GetProperty("name").GetString()!,
                row.GetProperty("email").GetString()!,
                row.GetProperty("pictureUrl").GetString(),
                row.GetProperty("avatar").Deserialize<AvatarView>(AppJsonOptions())!,
                row.GetProperty("access").GetString()!,
                row.GetProperty("isAdmin").GetBoolean(),
                row.GetProperty("isSelf").GetBoolean(),
                row.GetProperty("requestedLabel").GetString()!,
                row.GetProperty("changedLabel").GetString(),
                row.GetProperty("changedBy").GetString()))
            .ToList();
        var view = new AdminUsersView(users, example.GetProperty("pendingCount").GetInt32(), example.GetProperty("adminCount").GetInt32());
        var json = Serialise(view);

        JsonShape.AssertMatches(json, "admin-users.json", ScreenShapes.AdminUsers);
        SameJson(json, example).ShouldBeTrue(Differs(json, example));
        users.Select(row => row.Access).ShouldBe(["pending", "approved", "approved", "rejected"]);
    }

    private static DeckPageSlide Slide(string layout, string slots, int position = 0) => DeckPageSlide.From(
        new Deck { Id = "7Kq2xYp9LmQa", Slug = "ship-smaller", Title = "Ship smaller", PartsJson = "[]", Template = "workshop", Palette = "cobalt" },
        new Slide { DeckId = "7Kq2xYp9LmQa", Position = position, Layout = layout, SlotsJson = slots },
        Workshop);

    // The template those decks are on, as ITemplates resolves it: it finds each slide's layout for the surface.
    private static ResolvedTemplate Workshop => BuiltInTemplates.Resolve(new TemplateCatalogue().Find("workshop")!);

    private static ActivityEntryRow Entry(string actor, ActorKind kind, ActorVia via, string? tool, string summary) => new()
    {
        DeckId = "7Kq2xYp9LmQa",
        ActorName = actor,
        ActorKind = kind.ToString(),
        Via = via.ToString(),
        Tool = tool,
        Summary = summary,
    };

    private IPaletteCatalogue Palettes => Factory.Services.GetRequiredService<IPaletteCatalogue>();

    private JsonSerializerOptions AppJsonOptions() =>
        Factory.Services.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

    private JsonElement Serialise<T>(T record) => JsonSerializer.SerializeToElement(record, AppJsonOptions());

    // The example shows notes as the owner reads them (D16): a deep dive's notes carry the skip prefix on read, and the
    // stored notes never do. Strip the prefix to get what the database holds, so the record test pins the prefix.
    private static string? StoredNotes(JsonElement slide)
    {
        var notes = slide.GetProperty("notes").GetString();
        const string PREFIX = "Deep dive. Skip if short of time. ";
        return slide.GetProperty("deepDive").GetBoolean() && notes!.StartsWith(PREFIX, StringComparison.Ordinal)
            ? notes[PREFIX.Length..]
            : notes;
    }

    private static JsonElement Example(string file) =>
        JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));

    private static bool SameJson(JsonElement actual, JsonElement example) =>
        JsonNode.DeepEquals(JsonNode.Parse(actual.GetRawText()), JsonNode.Parse(example.GetRawText()));

    private static string Differs(JsonElement actual, JsonElement example) =>
        $"The record's JSON differs from the example.\nRecord:  {actual.GetRawText()}\nExample: {example.GetRawText()}";
}
