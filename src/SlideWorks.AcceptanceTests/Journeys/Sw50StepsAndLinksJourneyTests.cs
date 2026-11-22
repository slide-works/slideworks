using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-50's links slide and strike-and-answer table on the real app. An agent builds each deck over MCP and the presenter
// sees it in the player. A hidden part keeps its box (visibility: hidden, never display: none), so each hidden answer and
// the hidden callout are read as drawn (computed visibility and opacity, a box with size) before they are shown, and the
// same locator is then shown to be visible. The answers are toned cells, so each sits in a block-badge inside the Reveal.
public class Sw50StepsAndLinksJourneyTests : AcceptanceTest
{
    private static readonly string[] CLAIMS = ["Velocity measures output", "More people go faster", "Estimates are promises"];
    private static readonly string[] ANSWERS = ["It measures effort", "Until they queue", "They are forecasts"];
    private const string CALLOUT = "Measure outcomes, not output.";

    [Test]
    public async Task Person_WhenClickingALink_ShouldNotAdvanceTheSlide()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Links {Guid.NewGuid():N}" });
        var deck = created.GetProperty("deck").GetString()!;
        var slug = new Uri(created.GetProperty("url").GetString()!).Segments[^1];
        var signIn = AcceptanceApp.BaseUrl + "/signin";
        var added = await AddAsync(agent, deck, "links", new
        {
            kicker = "Read next",
            headline = "Where to read more",
            links = new[]
            {
                new { kind = "Talk", title = "The first link", source = "Sam Carter, here", url = signIn },
                new { kind = "Article", title = "The second link", source = "Sam Carter, here", url = signIn },
            },
        });
        await AddAsync(agent, deck, "quote", new { quote = "The slide after the links.", attribution = "Sam Carter" });
        added.GetProperty("steps").GetInt32().ShouldBe(0);
        await AgentSeed.CallAsync(agent, "set_sharing", new() { ["deck"] = deck, ["level"] = "link" });

        // The refusal: an address that is not http or https, and nothing changes.
        var refusal = await RefuseAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = "links",
            ["slots"] = JsonSerializer.SerializeToElement(new
            {
                headline = "Not a link",
                links = new[] { new { title = "Bad", url = "javascript:alert(1)" } },
            }),
        });
        refusal.ShouldContain("links.links[0].url");
        (await SlidesAsync(agent, deck)).Count.ShouldBe(2);

        // 1. The signed-in player: the click opens a new page at the address and the player stays on the links slide.
        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await OpenAsync(player, deck);
        await AssertLinksAsync(player, signIn, interactive: true);
        var playerPointer = await player.Slide(1).GetByTestId("links-title").First.EvaluateAsync<string>("a => getComputedStyle(a).pointerEvents");
        var requested = Track(Page.Context);
        var popup = await Page.RunAndWaitForPopupAsync(() => player.Slide(1).GetByTestId("links-title").First.ClickAsync());
        await Expect(popup).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(AcceptanceApp.BaseUrl) + "/"));
        requested.ShouldContain(signIn);
        await Expect(player.Count).ToHaveTextAsync("1 / 2");
        await Expect(player.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(player.Layer(2)).ToHaveAttributeAsync("data-active", "false");
        await popup.CloseAsync();

        // 2. The same through the deck's share link, anonymous in a browser of its own.
        await using var viewerContext = await Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = AcceptanceApp.BaseUrl });
        var viewerTab = await viewerContext.NewPageAsync();
        var viewer = new PlayerPage(viewerTab);
        await viewer.GotoAsync(deck, slug);
        await Expect(viewer.Count).ToHaveTextAsync("1 / 2");
        await Expect(viewer.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await AssertLinksAsync(viewer, signIn, interactive: true);
        var viewerRequested = Track(viewerContext);
        var viewerPopup = await viewerTab.RunAndWaitForPopupAsync(() => viewer.Slide(1).GetByTestId("links-title").Last.ClickAsync());
        await Expect(viewerPopup).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(AcceptanceApp.BaseUrl) + "/signin"));
        viewerRequested.ShouldContain(signIn);
        await Expect(viewer.Count).ToHaveTextAsync("1 / 2");
        await Expect(viewer.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await Expect(viewer.Layer(2)).ToHaveAttributeAsync("data-active", "false");

        // 3. The deck page's thumbnail draws the same anchors, out of the tab order and out of reach of a click.
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deck);
        await Expect(deckPage.Thumbnails).ToHaveCountAsync(2);
        var anchors = deckPage.Thumbnail(1).GetByTestId("links-title");
        await Expect(anchors).ToHaveCountAsync(2);
        await Expect(anchors.First).ToHaveAttributeAsync("tabindex", "-1");
        await Expect(anchors.Last).ToHaveAttributeAsync("tabindex", "-1");
        await Expect(anchors.First).ToHaveAttributeAsync("href", signIn);
        (await anchors.First.EvaluateAsync<string>("a => getComputedStyle(a).pointerEvents")).ShouldBe("none");
        // The same probe finds the player's anchor clickable, so "none" here means something.
        playerPointer.ShouldNotBe("none");
    }

    [Test]
    public async Task Person_WhenSteppingThroughAStrikeAndAnswerTable_ShouldStrikeEachClaimThenShowTheCallout()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = (await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Strike {Guid.NewGuid():N}" })).GetProperty("deck").GetString()!;
        var added = await AddAsync(agent, deck, "table", new
        {
            headline = "Three claims, three answers",
            columns = new[] { "Claim", "Answer" },
            rows = CLAIMS.Select((claim, i) => new object[] { claim, new { text = ANSWERS[i], tone = "good" } }).ToArray(),
            reveal = "strike-and-answer",
            callout = new { tone = "info", text = CALLOUT, reveal = "last" },
        });
        await AddAsync(agent, deck, "quote", new { quote = "The slide after the table.", attribution = "Sam Carter" });
        added.GetProperty("steps").GetInt32().ShouldBe(4);
        (await SlidesAsync(agent, deck)).Select(s => s.GetProperty("steps").GetInt32()).ShouldBe([4, 0]);

        // The refusal: three columns cannot be struck and answered, and the deck keeps its two slides.
        var refusal = await RefuseAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = "table",
            ["slots"] = JsonSerializer.SerializeToElement(new
            {
                headline = "Too wide",
                columns = new[] { "A", "B", "C" },
                rows = new[] { new[] { "1", "2", "3" } },
                reveal = "strike-and-answer",
            }),
        });
        refusal.ShouldContain("table.reveal: 'strike-and-answer' needs exactly 2 columns (got 3)");
        (await SlidesAsync(agent, deck)).Count.ShouldBe(2);

        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await OpenAsync(player, deck);
        var slide = player.Slide(1);
        var probe = new SlideProbe(Page, 1);
        var cells = slide.GetByTestId("table-cell");
        var answers = slide.GetByTestId("block-badge");
        var callout = slide.GetByTestId("block-callout");

        // Step 0: the page has shown every claim; the answers and the callout are in the DOM, drawn hidden, each with a box.
        await Expect(slide.GetByTestId("table-row")).ToHaveCountAsync(3);
        foreach (var row in Enumerable.Range(0, 3))
        {
            await Expect(cells.Nth(row * 2)).ToHaveTextAsync(CLAIMS[row]);
            await Expect(cells.Nth(row * 2)).ToBeVisibleAsync();
        }
        await Expect(answers).ToHaveCountAsync(3);
        await Expect(callout).ToHaveCountAsync(1);
        foreach (var row in Enumerable.Range(0, 3))
        {
            await Expect(answers.Nth(row)).ToBeHiddenAsync();
            (await probe.ComputedAsync("block-badge", "visibility", row)).ShouldBe("hidden");
            (await probe.SettledBoxAsync("block-badge", row)).ShouldNotBeNull().Width.ShouldBeGreaterThan(0);
            (await probe.ComputedAsync("table-cell", "text-decoration-line", row * 2)).ShouldBe("none");
            (await probe.ComputedAsync("table-row", "opacity", row)).ShouldBe("1");
        }
        await Expect(callout).ToBeHiddenAsync();
        (await probe.ComputedAsync("block-callout", "visibility")).ShouldBe("hidden");
        var hiddenCallout = (await probe.SettledBoxAsync("block-callout")).ShouldNotBeNull();
        hiddenCallout.Height.ShouldBeGreaterThan(0);
        var firstRowBox = (await probe.SettledBoxAsync("table-row", 0)).ShouldNotBeNull();
        firstRowBox.Height.ShouldBeGreaterThan(0);
        var hiddenAnswers = new List<SlideProbe.Box>();
        foreach (var row in Enumerable.Range(0, 3))
        {
            hiddenAnswers.Add((await probe.SettledBoxAsync("block-badge", row)).ShouldNotBeNull());
        }

        // Clicks 1 to 3: row n's answer shows and its claim is struck; the rows before it dim; nothing moves.
        foreach (var n in Enumerable.Range(1, 3))
        {
            await Page.Keyboard.PressAsync("ArrowRight");
            await Expect(answers.Nth(n - 1)).ToHaveTextAsync(ANSWERS[n - 1]);
            await Expect(answers.Nth(n - 1)).ToBeVisibleAsync();
            await Expect(cells.Nth((n - 1) * 2)).ToHaveAttributeAsync("data-struck", "true");
            (await probe.ComputedAsync("table-cell", "text-decoration-line", (n - 1) * 2)).ShouldNotBeNull().ShouldContain("line-through");
            foreach (var later in Enumerable.Range(n, 3 - n))
            {
                await Expect(answers.Nth(later)).ToBeHiddenAsync();
                await Expect(cells.Nth(later * 2)).Not.ToHaveAttributeAsync("data-struck", "true");
                (await probe.ComputedAsync("table-cell", "text-decoration-line", later * 2)).ShouldBe("none");
            }
            foreach (var earlier in Enumerable.Range(0, n - 1))
            {
                await Expect(slide.GetByTestId("table-row").Nth(earlier)).ToHaveAttributeAsync("data-dim", "true");
                (await probe.ComputedAsync("table-row", "opacity", earlier)).ShouldBe("0.45");
            }
            await Expect(slide.GetByTestId("table-row").Nth(n - 1)).Not.ToHaveAttributeAsync("data-dim", "true");
            await Expect(callout).ToBeHiddenAsync();
            await AssertSameBoxAsync(probe, "block-badge", n - 1, hiddenAnswers[n - 1]);
            await AssertSameBoxAsync(probe, "table-row", 0, firstRowBox);
        }

        // Click 4: the callout shows under the table, in the place it held while hidden.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(callout).ToHaveTextAsync(CALLOUT);
        await Expect(callout).ToBeVisibleAsync();
        await AssertSameBoxAsync(probe, "block-callout", 0, hiddenCallout);
        await AssertSameBoxAsync(probe, "table-row", 0, firstRowBox);
        var lastRow = (await probe.SettledBoxAsync("table-row", 2)).ShouldNotBeNull();
        hiddenCallout.Top.ShouldBeGreaterThanOrEqualTo(lastRow.Bottom - 0.5);
        await Expect(player.Count).ToHaveTextAsync("1 / 2");

        // Click 5: on to the next slide.
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(player.Count).ToHaveTextAsync("2 / 2");
        await Expect(player.Layer(2)).ToHaveAttributeAsync("data-active", "true");
        (await SlidesAsync(agent, deck)).Select(s => s.GetProperty("steps").GetInt32()).ShouldBe([4, 0]);
    }

    // Both anchors are drawn as links to the address, open in a new tab and are in the tab order and clickable.
    private async Task AssertLinksAsync(PlayerPage player, string address, bool interactive)
    {
        var anchors = player.Slide(1).GetByTestId("links-title");
        await Expect(anchors).ToHaveCountAsync(2);
        await Expect(anchors.First).ToHaveTextAsync("The first link");
        await Expect(anchors.Last).ToHaveTextAsync("The second link");
        await Expect(anchors.First).ToBeVisibleAsync();
        foreach (var index in new[] { 0, 1 })
        {
            await Expect(anchors.Nth(index)).ToHaveAttributeAsync("href", address);
            await Expect(anchors.Nth(index)).ToHaveAttributeAsync("target", "_blank");
            await Expect(anchors.Nth(index)).ToHaveAttributeAsync("rel", "noopener noreferrer");
            (await anchors.Nth(index).GetAttributeAsync("tabindex") is null).ShouldBe(interactive);
        }
    }

    // Every address any page of the context asks for, in order.
    private static List<string> Track(IBrowserContext context)
    {
        var seen = new List<string>();
        context.Request += (_, request) =>
        {
            lock (seen)
            {
                seen.Add(request.Url);
            }
        };
        return seen;
    }

    private static async Task AssertSameBoxAsync(SlideProbe slide, string testId, int nth, SlideProbe.Box before)
    {
        var after = await Eventually.ReadAsync(() => slide.BoxAsync(testId, nth), b => b is not null && b.IsNear(before));
        after.ShouldNotBeNull().IsNear(before).ShouldBeTrue($"{testId} {nth} moved: {before} became {after}.");
    }

    private async Task OpenAsync(PlayerPage player, string deck)
    {
        await player.GotoAsync(deck);
        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.Count).ToHaveTextAsync("1 / 2");
        await Expect(player.Layer(1)).ToHaveAttributeAsync("data-active", "true");
        await player.FontsReadyAsync();
    }

    private static async Task<JsonElement> AddAsync(McpClient agent, string deck, string layout, object slots)
    {
        var added = await AgentSeed.CallAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = layout,
            ["slots"] = JsonSerializer.SerializeToElement(slots, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
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
}
