using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-50's layout journeys on the real app: an agent builds decks over MCP with the wave-1 layouts (dark surface, statement,
// quote with a stat, four cards, compare with stamps, a frameless image, parts with a mark), and the signed-in presenter
// sees each in the player. Every deck is the agent's own, with a unique title. A step that hides something keeps its box,
// so each hidden thing is shown to hold its place before it is shown. An absence (no tracker on a statement, no journey
// on an appendix divider) is checked only once the same slide shows its footer, then two animation frames later, with a
// probe over the whole slide that, in the same test, finds the tracker or journey on a slide that should draw it.
public class Sw50LayoutJourneyTests : AcceptanceTest
{
    private const string FOLLOW_UP = "Small slices came back with an answer in a week.";
    private const string PIN = "Hold on to this one";

    [Test]
    public async Task Agent_WhenAddingADarkQuote_ShouldShowNightWithTrackerAndFooter()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CreateDeckAsync(agent, "Dark quote", ["Why", "How"]);

        var quote = await AddAsync(agent, deck, "quote", new
        {
            kicker = "Evidence",
            headline = "What a team said after one month",
            quote = "We ship every day now and nobody is afraid of Friday.",
            attribution = "Priya Nair",
            emphasis = new[] { new { text = "ship every day", tone = "accent", @in = "quote" } },
            stat = new { value = "4 months", caption = "Down from a quarterly release" },
            follow_up = FOLLOW_UP,
            reveal = "one-by-one",
        }, part: 1, surface: "dark");
        var statement = await AddAsync(agent, deck, "statement", new
        {
            statement = "Ship the smallest thing that answers the question.",
            sub = "Then ask again.",
            reveal = "one-by-one",
        }, part: 1, surface: "dark");
        await AddAsync(agent, deck, "quote", new { quote = "A light quote for comparison.", attribution = "Sam Rivera" }, part: 1);

        quote.GetProperty("steps").GetInt32().ShouldBe(2);
        statement.GetProperty("steps").GetInt32().ShouldBe(1);
        quote.TryGetProperty("surface", out _).ShouldBeFalse();
        var slides = await SlidesAsync(agent, deck);
        slides.Select(s => s.GetProperty("surface").GetString()).ShouldBe(["dark", "dark", "light"]);
        slides.Select(s => s.GetProperty("steps").GetInt32()).ShouldBe([2, 1, 0]);

        // The refusal: compare is light only, and nothing changes.
        var refusal = await RefuseAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = "compare",
            ["slots"] = JsonSerializer.SerializeToElement(CompareSlots(stamped: false)),
            ["surface"] = "dark",
        });
        refusal.ShouldContain("compare.surface: this layout is light only");
        (await SlidesAsync(agent, deck)).Count.ShouldBe(3);

        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await OpenAsync(player, deck, 1, 3);
        var darkQuote = new SlideProbe(Page, 1);
        var darkStatement = new SlideProbe(Page, 2);
        var lightQuote = new SlideProbe(Page, 3);

        (await Eventually.ReadAsync(darkQuote.SurfaceAsync, s => s == "dark")).ShouldBe("dark");
        (await darkStatement.SurfaceAsync()).ShouldBe("dark");
        (await lightQuote.SurfaceAsync()).ShouldBe("light");
        var darkBackground = await darkQuote.ComputedAsync("slide-frame", "background-color");
        var lightBackground = await lightQuote.ComputedAsync("slide-frame", "background-color");
        darkBackground.ShouldNotBeNullOrEmpty();
        darkBackground.ShouldNotBe(lightBackground);

        // The quote draws the tracker and the footer, on a dark slide.
        var quoteChrome = await darkQuote.ChromeAsync();
        quoteChrome.Tracker.ShouldBeTrue();
        quoteChrome.Footer.ShouldBeTrue();
        await Expect(player.Slide(1).GetByTestId("part-tracker-part")).ToHaveCountAsync(2);

        // The emphasised phrase differs in colour from the quote's own ink.
        await Expect(player.Slide(1).GetByTestId("emphasis")).ToHaveTextAsync("ship every day");
        (await darkQuote.ComputedAsync("emphasis", "color")).ShouldNotBe(await darkQuote.ComputedAsync("quote-text", "color"));

        // Two clicks: the stat, then the follow-up. Each hidden one keeps its box.
        await Expect(player.Slide(1).GetByTestId("quote-stat")).ToBeHiddenAsync();
        await Expect(player.Slide(1).GetByTestId("quote-follow-up")).ToBeHiddenAsync();
        var hiddenStat = (await darkQuote.SettledBoxAsync("quote-stat")).ShouldNotBeNull();
        var hiddenFollowUp = (await darkQuote.SettledBoxAsync("quote-follow-up")).ShouldNotBeNull();
        hiddenStat.Height.ShouldBeGreaterThan(0);
        hiddenFollowUp.Height.ShouldBeGreaterThan(0);
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Slide(1).GetByTestId("quote-stat-value")).ToHaveTextAsync("4 months");
        await Expect(player.Slide(1).GetByTestId("quote-stat")).ToBeVisibleAsync();
        await Expect(player.Slide(1).GetByTestId("quote-follow-up")).ToBeHiddenAsync();
        await AssertSameBoxAsync(darkQuote, "quote-stat", hiddenStat);
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Slide(1).GetByTestId("quote-follow-up")).ToHaveTextAsync(FOLLOW_UP);
        await Expect(player.Slide(1).GetByTestId("quote-follow-up")).ToBeVisibleAsync();
        await AssertSameBoxAsync(darkQuote, "quote-follow-up", hiddenFollowUp);
        await AssertSameBoxAsync(darkQuote, "quote-stat", hiddenStat);

        // The statement: its sub is hidden until one click, and it draws no tracker anywhere on the slide.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync("2 / 3");
        await Expect(player.Layer(2)).ToHaveAttributeAsync("data-active", "true");
        await Expect(player.Slide(2).GetByTestId("statement-sub")).ToBeHiddenAsync();
        var hiddenSub = (await darkStatement.SettledBoxAsync("statement-sub")).ShouldNotBeNull();
        hiddenSub.Height.ShouldBeGreaterThan(0);
        await Expect(player.Slide(2).GetByTestId("slide-footer")).ToBeVisibleAsync();
        await NextFramesAsync();
        var statementChrome = await darkStatement.ChromeAsync();
        statementChrome.Footer.ShouldBeTrue();
        statementChrome.Tracker.ShouldBeFalse();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Slide(2).GetByTestId("statement-sub")).ToBeVisibleAsync();
        await AssertSameBoxAsync(darkStatement, "statement-sub", hiddenSub);

        // The deck page's thumbnail of the quote carries the dark surface.
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deck);
        await Expect(deckPage.Thumbnails).ToHaveCountAsync(3);
        await Expect(deckPage.Thumbnail(1).GetByTestId("slide-frame")).ToHaveAttributeAsync("data-surface", "dark");
        await Expect(deckPage.Thumbnail(3).GetByTestId("slide-frame")).ToHaveAttributeAsync("data-surface", "light");
    }

    [Test]
    public async Task Agent_WhenAddingFourCards_ShouldShowTwoByTwo()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CreateDeckAsync(agent, "Four cards", []);

        // A card with an icon and a pin in a 2x2 grid is too tall to leave room for a callout under it (the server's fit
        // rule refuses it), so the pin and icon go on one four-card slide and the callout on a second.
        var pinned = await AddAsync(agent, deck, "cards", new
        {
            headline = "Four things to hold on to",
            cards = new object[]
            {
                new { title = "Slice thin", text = "A usable card.", tone = "positive", fill = "solid", pin = PIN, icon = "bulb" },
                new { title = "Demo weekly", text = "Show weekly." },
                new { title = "Hide behind a flag", text = "Ship dark." },
                new { title = "Learn early", text = "Answers steer." },
            },
        });
        var called = await AddAsync(agent, deck, "cards", new
        {
            headline = "Four more, with a closing line",
            cards = new object[]
            {
                new { title = "One", text = "First." },
                new { title = "Two", text = "Second." },
                new { title = "Three", text = "Third." },
                new { title = "Four", text = "Fourth." },
            },
            callout = new { tone = "positive", text = "Thin beats thick.", reveal = "last" },
        });
        var compare = await AddAsync(agent, deck, "compare", CompareSlots(stamped: true));

        pinned.GetProperty("steps").GetInt32().ShouldBe(0);
        called.GetProperty("steps").GetInt32().ShouldBe(1);
        compare.GetProperty("steps").GetInt32().ShouldBe(2);
        (await SlidesAsync(agent, deck)).Select(s => s.GetProperty("steps").GetInt32()).ShouldBe([0, 1, 2]);

        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await OpenAsync(player, deck, 1, 3);
        var slide = new SlideProbe(Page, 1);
        var cardSlide = player.Slide(1);

        // Four boxes in two rows and two columns.
        await Expect(cardSlide.GetByTestId("cards-card")).ToHaveCountAsync(4);
        (await slide.BoxesAsync("cards-card")).Count.ShouldBe(4);
        await AssertTwoByTwoAsync(slide);

        // The pin's text, and the icon as an svg.
        await Expect(cardSlide.GetByTestId("pin")).ToHaveTextAsync(PIN);
        await Expect(cardSlide.GetByTestId("block-icon")).ToHaveCountAsync(1);
        (await slide.TagAsync("block-icon")).ShouldBe("svg");

        // The second slide: the callout keeps its box while hidden, shows at the last step, and sits under every card.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync("2 / 3");
        await Expect(player.Layer(2)).ToHaveAttributeAsync("data-active", "true");
        var second = new SlideProbe(Page, 2);
        await Expect(player.Slide(2).GetByTestId("cards-card")).ToHaveCountAsync(4);
        await AssertTwoByTwoAsync(second);
        await Expect(player.Slide(2).GetByTestId("block-callout")).ToBeHiddenAsync();
        var hiddenBox = (await second.SettledBoxAsync("block-callout")).ShouldNotBeNull();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Slide(2).GetByTestId("block-callout")).ToBeVisibleAsync();
        var (cardBoxes, shownBox) = await Eventually.ReadAsync(
            async () => (await second.BoxesAsync("cards-card"), await second.BoxAsync("block-callout")),
            r => r.Item2 is { } shown && shown.IsNear(hiddenBox) && r.Item1.Count == 4 && shown.Top >= r.Item1.Max(b => b.Bottom) - 0.5);
        shownBox.ShouldNotBeNull().IsNear(hiddenBox).ShouldBeTrue($"The callout moved on reveal: {hiddenBox} became {shownBox}.");
        shownBox.Top.ShouldBeGreaterThanOrEqualTo(cardBoxes.Max(b => b.Bottom) - 0.5);

        // Compare: the failure panel alone, then the success panel, then both stamps on one step.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync("3 / 3");
        await Expect(player.Layer(3)).ToHaveAttributeAsync("data-active", "true");
        var compareSlide = player.Slide(3);
        await Expect(compareSlide.GetByTestId("compare-failure")).ToBeVisibleAsync();
        await Expect(compareSlide.GetByTestId("compare-success")).ToBeHiddenAsync();
        await Expect(compareSlide.GetByTestId("block-stamp")).ToHaveCountAsync(2);
        await Expect(compareSlide.GetByTestId("block-stamp").First).ToBeHiddenAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(compareSlide.GetByTestId("compare-success")).ToBeVisibleAsync();
        await Expect(compareSlide.GetByTestId("block-stamp").First).ToBeHiddenAsync();
        await Expect(compareSlide.GetByTestId("block-stamp").Last).ToBeHiddenAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(compareSlide.GetByTestId("block-stamp").First).ToBeVisibleAsync();
        await Expect(compareSlide.GetByTestId("block-stamp").Last).ToBeVisibleAsync();
        await Expect(compareSlide.GetByTestId("block-stamp").First).ToHaveTextAsync("Draft");
        await Expect(compareSlide.GetByTestId("block-stamp").Last).ToHaveTextAsync("Shipped");
    }

    // The element's box now equals its box while hidden (x, y, width, height within half a pixel): revealing shifted nothing.
    private static async Task AssertSameBoxAsync(SlideProbe slide, string testId, SlideProbe.Box hidden)
    {
        var shown = await Eventually.ReadAsync(() => slide.BoxAsync(testId), b => b is not null && b.IsNear(hidden));
        shown.ShouldNotBeNull().IsNear(hidden).ShouldBeTrue($"{testId} moved on reveal: {hidden} became {shown}.");
    }

    private static async Task AssertTwoByTwoAsync(SlideProbe slide)
    {
        var boxes = await Eventually.ReadAsync(
            () => slide.BoxesAsync("cards-card"),
            b => b.Count == 4 && b.Select(x => Math.Round(x.Top)).Distinct().Count() == 2 && b.Select(x => Math.Round(x.Left)).Distinct().Count() == 2);
        boxes.Select(b => Math.Round(b.Top)).Distinct().Count().ShouldBe(2);
        boxes.Select(b => Math.Round(b.Left)).Distinct().Count().ShouldBe(2);
    }

    [Test]
    public async Task Agent_WhenAPartHasAMark_ShouldShowItsDividerOffTheTracker()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var parts = new object[]
        {
            new { name = "Why retros matter", @short = "Why", caption = "The reason" },
            new { name = "How to run one", @short = "How", caption = "The method" },
            new { name = "Appendix", mark = "A" },
        };

        // A marked part before a numbered one is refused, and no deck is made.
        var decksBefore = await DeckCountAsync(agent);
        var refusal = await RefuseAsync(agent, "create_deck", new()
        {
            ["title"] = $"Parts refused {Guid.NewGuid():N}",
            ["parts"] = JsonSerializer.SerializeToElement(new object[] { "One", new { name = "Appendix", mark = "A" }, "Three" }),
        });
        refusal.ShouldContain("parts[1]: a part with a mark must come after every numbered part");
        (await DeckCountAsync(agent)).ShouldBe(decksBefore);

        var deck = await CreateDeckAsync(agent, "Marked part", parts);
        await AddAsync(agent, deck, "divider", new { title = "Why retros matter" }, part: 1);
        await AddAsync(agent, deck, "bullets", new { headline = "Why we meet", items = new[] { "To learn", "To change" } }, part: 1);
        await AddAsync(agent, deck, "divider", new { title = "Appendix" }, part: 3);
        await AddAsync(agent, deck, "bullets", new { headline = "Extra notes", items = new[] { "Raw votes", "Old actions" } }, part: 3);

        var got = (await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = deck })).GetProperty("parts").EnumerateArray().ToList();
        got.Select(p => p.GetProperty("name").GetString()).ShouldBe(["Why retros matter", "How to run one", "Appendix"]);
        got.Select(p => p.GetProperty("short").GetString()).Take(2).ShouldBe(["Why", "How"]);
        got.Select(p => p.GetProperty("caption").GetString()).Take(2).ShouldBe(["The reason", "The method"]);
        got[2].GetProperty("mark").GetString().ShouldBe("A");

        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await OpenAsync(player, deck, 1, 4);

        // The first divider's journey shows the captions, one stop per numbered part.
        var firstDivider = new SlideProbe(Page, 1);
        await Expect(player.Slide(1).GetByTestId("journey-part")).ToHaveCountAsync(2);
        await Expect(player.Slide(1).GetByTestId("journey-part").First).ToContainTextAsync("The reason");
        await Expect(player.Slide(1).GetByTestId("journey-part").Last).ToContainTextAsync("The method");
        (await firstDivider.ChromeAsync()).Journey.ShouldBeTrue();

        // A numbered content slide draws the tracker with the short names. Only the slide showing is drawn, so move to it.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync("2 / 4");
        await Expect(player.Layer(2)).ToHaveAttributeAsync("data-active", "true");
        await Expect(player.Slide(2).GetByTestId("slide-footer")).ToBeVisibleAsync();
        var numbered = new SlideProbe(Page, 2);
        await Expect(player.Slide(2).GetByTestId("part-tracker-part")).ToHaveTextAsync(["Why", "How"]);
        (await numbered.ChromeAsync()).Tracker.ShouldBeTrue();

        // The appendix divider shows its mark and no journey; the appendix slide draws no tracker. Absence is read
        // once the appendix slides show their own text (and the content slide its footer), then two frames later.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync("3 / 4");
        await Expect(player.Layer(3)).ToHaveAttributeAsync("data-active", "true");
        await Expect(player.Slide(3).GetByTestId("divider-number")).ToHaveTextAsync("A");
        await Expect(player.Slide(3).GetByTestId("divider-title")).ToBeVisibleAsync();
        await NextFramesAsync();
        (await new SlideProbe(Page, 3).ChromeAsync()).Journey.ShouldBeFalse();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync("4 / 4");
        await Expect(player.Layer(4)).ToHaveAttributeAsync("data-active", "true");
        await Expect(player.Slide(4).GetByTestId("slide-footer")).ToBeVisibleAsync();
        await Expect(player.Slide(4).GetByTestId("bullets-item")).ToHaveCountAsync(2);
        await NextFramesAsync();
        var appendix = await new SlideProbe(Page, 4).ChromeAsync();
        appendix.Footer.ShouldBeTrue();
        appendix.Tracker.ShouldBeFalse();
    }

    [Test]
    public async Task Agent_WhenAddingAFramelessImage_ShouldShowItBesideTheHeadline()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CreateDeckAsync(agent, "Frameless image", []);
        var upload = await AgentSeed.CallAsync(agent, "upload_image", new() { ["deck"] = deck, ["name"] = "cutout.png", ["contentType"] = "image/png" });
        await PutImageAsync(upload.GetProperty("uploadUrl").GetString().ShouldNotBeNull(), SampleImage.Png());
        var imageId = upload.GetProperty("image").GetString().ShouldNotBeNull();

        await AddAsync(agent, deck, "image", new
        {
            headline = "The cut-out sits on the slide",
            image = new { image = imageId, alt = "A square gradient." },
            frame = "none",
            position = "right",
        });
        await AddAsync(agent, deck, "image", new
        {
            headline = "The framed one sits on a card",
            image = new { image = imageId, alt = "A square gradient." },
            position = "right",
        });

        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await OpenAsync(player, deck, 1, 2);
        await player.ImageLoadedAsync(1);
        await player.ImageLoadedAsync(2);

        // The same probe finds the card's shadow on the framed slide, so "none" on the frameless one means something.
        (await new SlideProbe(Page, 2).ComputedAsync("image-picture", "box-shadow")).ShouldNotBe("none");
        var slide = new SlideProbe(Page, 1);
        (await slide.ComputedAsync("image-picture", "box-shadow")).ShouldBe("none");
        var (headline, picture) = await Eventually.ReadAsync(
            async () => (await slide.BoxAsync("image-headline"), await slide.BoxAsync("image-picture")),
            r => r.Item1 is { } h && r.Item2 is { } p && p.Width > 0 && p.Left > h.Right);
        headline.ShouldNotBeNull();
        picture.ShouldNotBeNull();
        picture.Width.ShouldBeGreaterThan(0);
        picture.Left.ShouldBeGreaterThan(headline.Right);
    }

    private static object CompareSlots(bool stamped) => new
    {
        headline = "When the customer gets to answer",
        failure = new { label = "Failure Mode", title = "Slice by Layer", points = new[] { "Database, then API, then UI" }, stamp = stamped ? "Draft" : null },
        success = new { label = "Success Mode", title = "Slice by Customer Action", points = new[] { "Something to demo every week" }, stamp = stamped ? "Shipped" : null },
        reveal = stamped ? "one-by-one" : null,
    };

    private static async Task<string> CreateDeckAsync(McpClient agent, string name, object[] parts)
    {
        var args = new Dictionary<string, object?> { ["title"] = $"{name} {Guid.NewGuid():N}" };
        if (parts.Length > 0)
        {
            args["parts"] = JsonSerializer.SerializeToElement(parts);
        }
        return (await AgentSeed.CallAsync(agent, "create_deck", args)).GetProperty("deck").GetString().ShouldNotBeNull();
    }

    private static async Task<JsonElement> AddAsync(McpClient agent, string deck, string layout, object slots, int? part = null, string? surface = null)
    {
        var args = new Dictionary<string, object?>
        {
            ["deck"] = deck,
            ["layout"] = layout,
            ["slots"] = JsonSerializer.SerializeToElement(slots, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }),
        };
        if (part is not null)
        {
            args["part"] = part;
        }
        if (surface is not null)
        {
            args["surface"] = surface;
        }
        var added = await AgentSeed.CallAsync(agent, "add_slide", args);
        added.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        return added;
    }

    private static async Task<List<JsonElement>> SlidesAsync(McpClient agent, string deck) =>
        (await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = deck })).GetProperty("slides").EnumerateArray().ToList();

    private static async Task<int> DeckCountAsync(McpClient agent) =>
        (await AgentSeed.CallAsync(agent, "list_decks", new())).GetProperty("decks").GetArrayLength();

    // A tool call that must be refused: returns the tool's error text.
    private static async Task<string> RefuseAsync(McpClient agent, string tool, Dictionary<string, object?> args)
    {
        var result = await agent.CallToolAsync(tool, args);
        (result.IsError ?? false).ShouldBeTrue($"{tool} was not refused.");
        return string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
    }

    // Opens the player at slide 1 and waits until it is live: the count, the active layer and the fonts.
    private async Task OpenAsync(PlayerPage player, string deck, int first, int total)
    {
        await player.GotoAsync(deck);
        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.Count).ToHaveTextAsync($"{first} / {total}");
        await Expect(player.Layer(first)).ToHaveAttributeAsync("data-active", "true");
        await player.FontsReadyAsync();
    }

    private async Task NextFramesAsync() =>
        await Page.EvaluateAsync("() => new Promise(done => requestAnimationFrame(() => requestAnimationFrame(() => done(true))))");

    // The agent PUTs the raw bytes to the one-time upload URL, with no auth header, as upload_image says.
    private static async Task PutImageAsync(string uploadUrl, byte[] png)
    {
        using var http = new HttpClient();
        using var content = new ByteArrayContent(png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        using var response = await http.PutAsync(uploadUrl, content);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }
}
