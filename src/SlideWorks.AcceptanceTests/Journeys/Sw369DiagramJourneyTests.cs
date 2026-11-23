using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.AcceptanceTests.Journeys.TenSlideBrief;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-369's diagram journey on the real app: an agent adds the four diagram layouts (stack, bars, flow, levels) over MCP
// and a person sees them in the player as text, with no <img> under them and the bars drawn in the palette's colour; and
// a person steps through a flow whose hidden nodes keep their place. Every deck is the agent's own, with a unique title.
// The one absence checked, no <img> under the four slides, is made only once every slide's text is on screen and the
// image slide's picture has loaded, with a probe that finds the image slide's <img> in the same test.
public class Sw369DiagramJourneyTests : AcceptanceTest
{
    private const string REVEAL = "one-by-one";

    [Test]
    public async Task Agent_WhenAddingTheFourDiagrams_ShouldShowTextAndNoImage()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new()
        {
            ["title"] = $"Diagrams {Guid.NewGuid():N}",
            ["template"] = "talk",
            ["palette"] = "ember",
        });
        var deck = created.GetProperty("deck").GetString().ShouldNotBeNull();

        var stack = await AddAsync(agent, deck, "stack", WithReveal(Stack));
        var bars = await AddAsync(agent, deck, "bars", WithReveal(Bars));
        var flow = await AddAsync(agent, deck, "flow", WithReveal(Flow));
        var levels = await AddAsync(agent, deck, "levels", Levels);
        var upload = await AgentSeed.CallAsync(agent, "upload_image", new() { ["deck"] = deck, ["name"] = "map.png", ["contentType"] = "image/png" });
        await PutImageAsync(upload.GetProperty("uploadUrl").GetString().ShouldNotBeNull(), SampleImage.Png());
        await AddAsync(agent, deck, "image", Image(upload.GetProperty("image").GetString().ShouldNotBeNull()));

        // One step per segment, bar and node; the levels have none.
        stack.GetProperty("steps").GetInt32().ShouldBe(4);
        bars.GetProperty("steps").GetInt32().ShouldBe(4);
        flow.GetProperty("steps").GetInt32().ShouldBe(3);
        levels.GetProperty("steps").GetInt32().ShouldBe(0);
        var layouts = (await SlidesAsync(agent, deck)).Select(s => s.GetProperty("layout").GetString());
        layouts.ShouldBe(["stack", "bars", "flow", "levels", "image"]);

        // The refusal: bars are light only, and the deck keeps its five slides.
        var refusal = await RefuseAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = "bars",
            ["slots"] = JsonSerializer.SerializeToElement(Bars),
            ["surface"] = "dark",
        });
        refusal.ShouldContain("bars.surface: this layout is light only");
        (await SlidesAsync(agent, deck)).Count.ShouldBe(5);

        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await OpenAsync(player, deck, 5);

        // Walk the deck, watching the label inside each segment and bar (the segment and bar boxes themselves stay drawn, as a hidden
        // step keeps its box). On each slide the parts are hidden at step 0 and drawn one per click; once the slide's layer is the
        // active one and its last step is in, every part is visible, with its text.
        string[] segments = ["Waiting for review", "Waiting for a slot", "Building", "Testing"];
        string[] barNames = ["Review", "Build", "Test", "Deploy"];
        string[] nodes = ["Slice", "Ship", "Learn"];
        await StepThroughAsync(player, 1, "stack-label", segments);
        await Page.Keyboard.PressAsync("ArrowRight");
        await StepThroughAsync(player, 2, "bars-label", barNames);

        // The first bar is drawn in the palette's colour: a visible fill with a box, read once the bar is shown.
        var bar = new SlideProbe(Page, 2);
        var before = await Eventually.ReadAsync(() => bar.ComputedAsync("bars-fill", "background-color"), IsDrawn);
        IsDrawn(before).ShouldBeTrue(before);
        (await bar.SettledBoxAsync("bars-fill")).ShouldNotBeNull().Width.ShouldBeGreaterThan(0);

        await Page.Keyboard.PressAsync("ArrowRight");
        await StepThroughAsync(player, 3, "flow-node", nodes);
        await Page.Keyboard.PressAsync("ArrowRight");
        await ExpectShownAsync(player, 4, "levels-rung", ["Continuous flow", "Small batches", "Big launches"]);
        await ExpectShownAsync(player, 4, "facts-tile", ["Cycle time", "Slips"]);
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Layer(5)).ToHaveAttributeAsync("data-active", "true");
        await player.ImageLoadedAsync(5);

        // No <img> under the four, and the same probe finds the image slide's picture.
        for (var number = 1; number <= 4; number++)
        {
            (await ImageCountAsync(number)).ShouldBe(0, $"slide {number}");
        }
        (await ImageCountAsync(5)).ShouldBe(1);

        var look = await AgentSeed.CallAsync(agent, "set_look", new() { ["deck"] = deck, ["palette"] = "midnight" });
        look.GetProperty("look").GetProperty("palette").GetString().ShouldBe("midnight");
        await player.GotoAsync(deck, hash: "#2.4");
        await Expect(player.Count).ToHaveTextAsync("2 / 5");
        await player.FontsReadyAsync();
        await ExpectShownAsync(player, 2, "bars-label", barNames);
        var after = await Eventually.ReadAsync(() => bar.ComputedAsync("bars-fill", "background-color"), c => IsDrawn(c) && c != before);
        IsDrawn(after).ShouldBeTrue(after);
        after.ShouldNotBe(before);

        // The deck page shows four thumbnails with the four layout names, then the image.
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deck);
        await Expect(deckPage.Thumbnails).ToHaveCountAsync(5);
        string[] names = ["stack", "bars", "flow", "levels"];
        for (var number = 1; number <= 4; number++)
        {
            await Expect(deckPage.Thumbnail(number).GetByTestId("deck-thumb-layout")).ToBeVisibleAsync();
            await Expect(deckPage.Thumbnail(number).GetByTestId("deck-thumb-layout")).ToHaveTextAsync(names[number - 1]);
        }
    }

    [Test]
    public async Task Person_WhenSteppingThroughFlow_ShouldKeepTheFirstNodeStill()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Flow {Guid.NewGuid():N}" });
        var deck = created.GetProperty("deck").GetString().ShouldNotBeNull();
        var flow = await AddAsync(agent, deck, "flow", WithReveal(Flow));
        flow.GetProperty("steps").GetInt32().ShouldBe(3);

        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await OpenAsync(player, deck, 1);
        var nodes = player.Slide(1).GetByTestId("flow-node");
        var shown = nodes.Filter(new LocatorFilterOptions { Visible = true });
        var slide = new SlideProbe(Page, 1);

        // Before any click all three nodes are in the DOM, drawn hidden, each holding its place.
        await Expect(nodes).ToHaveCountAsync(3);
        await Expect(shown).ToHaveCountAsync(0);
        for (var node = 0; node < 3; node++)
        {
            (await Eventually.ReadAsync(() => slide.ComputedAsync("flow-node", "visibility", node), v => v == "hidden")).ShouldBe("hidden");
            (await slide.SettledBoxAsync("flow-node", node)).ShouldNotBeNull().Height.ShouldBeGreaterThan(0);
        }
        var first = (await slide.SettledBoxAsync("flow-node")).ShouldNotBeNull();
        first.Width.ShouldBeGreaterThan(0);

        // Three clicks show the nodes in turn, and the first never moves.
        for (var click = 1; click <= 3; click++)
        {
            await Page.Keyboard.PressAsync("ArrowRight");
            await Expect(shown).ToHaveCountAsync(click);
            (await slide.ComputedAsync("flow-node", "visibility", click - 1)).ShouldBe("visible");
            var now = (await slide.SettledBoxAsync("flow-node")).ShouldNotBeNull();
            now.IsNear(first).ShouldBeTrue($"after click {click} the first node is at {now}, was {first}");
        }
        await Expect(player.Count).ToHaveTextAsync("1 / 1");
    }

    private static Dictionary<string, object> WithReveal(IReadOnlyDictionary<string, object> slots) => new Dictionary<string, object>(slots) { ["reveal"] = REVEAL };

    // Clicks through a slide's steps: none of the parts is visible at first, and one more shows with each click.
    private async Task StepThroughAsync(PlayerPage player, int number, string testId, string[] parts)
    {
        await Expect(player.Layer(number)).ToHaveAttributeAsync("data-active", "true");
        var shown = player.Slide(number).GetByTestId(testId).Filter(new LocatorFilterOptions { Visible = true });
        await Expect(player.Slide(number).GetByTestId(testId)).ToHaveCountAsync(parts.Length);
        await Expect(shown).ToHaveCountAsync(0);
        for (var step = 1; step <= parts.Length; step++)
        {
            await Page.Keyboard.PressAsync("ArrowRight");
            await Expect(shown).ToHaveCountAsync(step);
        }
        await ExpectShownAsync(player, number, testId, parts);
    }

    // The slide's layer is the active one and every part is visible, with its text.
    private async Task ExpectShownAsync(PlayerPage player, int number, string testId, string[] parts)
    {
        await Expect(player.Layer(number)).ToHaveAttributeAsync("data-active", "true");
        var all = player.Slide(number).GetByTestId(testId);
        await Expect(all.Filter(new LocatorFilterOptions { Visible = true })).ToHaveCountAsync(parts.Length);
        await Expect(all).ToContainTextAsync(parts);
    }

    private async Task<int> ImageCountAsync(int number) =>
        await Page.EvaluateAsync<int>("n => document.querySelector(`[data-testid=\"slide-${n}\"]`).querySelectorAll('img').length", number);

    // A colour the browser really paints: not empty and not fully transparent.
    private static bool IsDrawn(string? colour) => !string.IsNullOrEmpty(colour) && colour != "rgba(0, 0, 0, 0)" && colour != "transparent";

    private async Task OpenAsync(PlayerPage player, string deck, int total)
    {
        await player.GotoAsync(deck);
        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.Count).ToHaveTextAsync($"1 / {total}");
        await Expect(player.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await player.FontsReadyAsync();
    }

    private static async Task<JsonElement> AddAsync(McpClient agent, string deck, string layout, object slots)
    {
        var added = await AgentSeed.CallAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = layout,
            ["slots"] = JsonSerializer.SerializeToElement(slots),
        });
        added.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        return added;
    }

    private static async Task<List<JsonElement>> SlidesAsync(McpClient agent, string deck) =>
        (await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = deck })).GetProperty("slides").EnumerateArray().ToList();

    // A tool call that must be refused: returns the tool's error text.
    private static async Task<string> RefuseAsync(McpClient agent, string tool, Dictionary<string, object?> args)
    {
        var result = await agent.CallToolAsync(tool, args);
        (result.IsError ?? false).ShouldBeTrue($"{tool} was not refused.");
        return string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
    }

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
