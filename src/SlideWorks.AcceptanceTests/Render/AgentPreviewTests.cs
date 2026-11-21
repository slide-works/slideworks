using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Render;

// D21 end to end: an agent calls preview_deck over MCP, then follows the returned instructions in its own headless
// Chromium. The shell steps run here as the equivalent Playwright calls: a fresh anonymous context at the viewport the
// result names, wait for wait_for on render_url with {slide} filled in for each number, then screenshot the viewport.
public class AgentPreviewTests : AcceptanceTest
{
    [Test]
    public async Task AgentFollowsPreviewInstructions_WhenTwoSlides_ShouldScreenshotEachAt1920x1080()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = (await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = "Preview me" })).GetProperty("deck").GetString()!;
        await AgentSeed.AddQuotesAsync(TestUser.Sam, deck, "Small batches ship sooner.", "Measure what the customer sees.");

        var preview = await AgentSeed.CallAsync(agent, "preview_deck", new() { ["deck"] = deck });

        var slides = preview.GetProperty("slides").EnumerateArray().Select(s => s.GetInt32()).ToList();
        slides.ShouldBe([1, 2]);
        preview.GetProperty("render_url").GetString()!.ShouldContain("{slide}");
        var shots = await AgentSeed.ScreenshotPreviewAsync(Browser, preview);

        foreach (var shot in shots.Values)
        {
            new Uri(shot.Url).Fragment.ShouldBeEmpty();
        }

        foreach (var png in shots.Values.Select(shot => shot.Png))
        {
            AgentSeed.PngSize(png).ShouldBe((1920, 1080));
        }
        shots[1].Png.SequenceEqual(shots[2].Png).ShouldBeFalse("The two slides rendered the same PNG.");
    }
}
