using System.Text.Json;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-2's agent journey across every lane's real code: an agent makes a workshop deck in Ember over MCP (create_deck
// returns the template's layouts), adds a divider, a deep-dive slide and a toned callout, previews them by following
// preview_deck's instructions in its own Chromium (D21), switches the palette with set_look (every slide re-skins), and
// the presenter sees the deep-dive pill and the divider's list in the player and one activity entry per change.
public class Sw2AgentJourneyTests : AcceptanceTest
{
    private const string DEEP_DIVE = "How the vote is counted";

    [Test]
    public async Task AgentBuildsWorkshopDeckInEmberWithDeepDivesAndPreviewsIt_WhenSwitchedToMidnight_ShouldShowDeepDivesAndLogEachCall()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);

        // 1. create_deck on the workshop template in Ember (AC: create_deck takes template and palette, returns layouts).
        var created = await AgentSeed.CallAsync(agent, "create_deck", new()
        {
            ["title"] = "Running a retro",
            ["parts"] = new[] { "Why", "How" },
            ["template"] = "workshop",
            ["palette"] = "ember",
        });
        var deck = created.GetProperty("deck").GetString()!;
        created.GetProperty("look").GetProperty("template").GetString().ShouldBe("workshop");
        created.GetProperty("look").GetProperty("palette").GetString().ShouldBe("ember");
        var layouts = created.GetProperty("layouts").EnumerateArray()
            .Select(l => l.ValueKind == JsonValueKind.String ? l.GetString() : l.GetProperty("name").GetString()).ToList();
        layouts.ShouldContain("divider");
        layouts.ShouldContain("bullets");

        // 2. A divider for part 1, a deep dive in it, and a bullets slide with a solid positive callout (AC: deep_dive, tone and fill).
        await AddSlideAsync(agent, deck, "divider", new { title = "Why retros matter" }, deepDive: false);
        await AddSlideAsync(agent, deck, "bullets", new { headline = DEEP_DIVE, items = new[] { "Dot votes, three each", "Ties go to the newest topic" } }, deepDive: true);
        await AddSlideAsync(agent, deck, "bullets", new
        {
            headline = "Keep it to an hour",
            items = new[] { "Set the stage", "Gather data", "Decide one action" },
            callout = new { tone = "positive", fill = "solid", text = "One action, owned, beats five ideas." },
        }, deepDive: false);

        // 3. preview_deck, then its instructions in the acceptance project's Chromium (AC: preview_deck; D21).
        var ember = await PreviewAsync(agent, deck, 2, 3);
        foreach (var png in ember.Values)
        {
            AgentSeed.PngSize(png).ShouldBe((1920, 1080));
        }
        ember[2].SequenceEqual(ember[3]).ShouldBeFalse("The deep dive and the callout slide rendered the same PNG.");

        // 4. set_look to Midnight: one change, no layout warnings, and the callout slide re-skins (AC: look re-skins every slide).
        var look = await AgentSeed.CallAsync(agent, "set_look", new() { ["deck"] = deck, ["palette"] = "midnight" });
        look.GetProperty("look").GetProperty("palette").GetString().ShouldBe("midnight");
        look.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        var midnight = await PreviewAsync(agent, deck, 3);
        AgentSeed.PngSize(midnight[3]).ShouldBe((1920, 1080));
        midnight[3].SequenceEqual(ember[3]).ShouldBeFalse("Slide 3 rendered the same PNG in Midnight as in Ember.");

        // 5. The player: the divider lists its part's deep dive, and the deep-dive slide shows the pill (AC: deep_dive).
        await SignIn.AsAsync(Page, TestUser.Sam);
        var player = new PlayerPage(Page);
        await player.GotoAsync(deck);
        await Expect(player.Slide(1).GetByTestId("divider-deep-dive")).ToHaveTextAsync([DEEP_DIVE]);
        await player.GotoAsync(deck, hash: "#2");
        await Expect(player.Slide(2).GetByTestId("deep-dive-pill")).ToBeVisibleAsync();
        await Expect(player.Slide(3).GetByTestId("deep-dive-pill")).ToHaveCountAsync(0);

        // 6. One activity entry per change, newest first; previews write none (AC: every look change writes one entry).
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deck);
        await Expect(deckPage.ActivityRows).ToHaveCountAsync(5);
        await Expect(deckPage.ActivityRows.Nth(0)).ToContainTextAsync("changed the palette to Midnight");
        await Expect(deckPage.ActivityRows.Nth(0)).ToContainTextAsync("set_look");
        await Expect(deckPage.ActivityRows.Nth(2)).ToContainTextAsync("added slide 2 (bullets) as a deep dive");
        await Expect(deckPage.ActivityRows.Nth(4)).ToContainTextAsync("created the deck from the workshop template with the Ember palette");
    }

    private static async Task AddSlideAsync(ModelContextProtocol.Client.McpClient agent, string deck, string layout, object slots, bool deepDive)
    {
        var added = await AgentSeed.CallAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = layout,
            ["part"] = 1,
            ["slots"] = JsonSerializer.SerializeToElement(slots),
            ["deep_dive"] = deepDive,
        });
        added.GetProperty("warnings").GetArrayLength().ShouldBe(0);
    }

    private async Task<Dictionary<int, byte[]>> PreviewAsync(ModelContextProtocol.Client.McpClient agent, string deck, params int[] slides)
    {
        var preview = await AgentSeed.CallAsync(agent, "preview_deck", new() { ["deck"] = deck, ["slides"] = slides });
        preview.GetProperty("slides").EnumerateArray().Select(s => s.GetProperty("number").GetInt32()).ShouldBe(slides);
        return await AgentSeed.ScreenshotPreviewAsync(Browser, preview);
    }
}
