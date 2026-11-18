using System.Buffers.Binary;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
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
        var deck = (await CallAsync(agent, "create_deck", new() { ["title"] = "Preview me" })).GetProperty("deck").GetString()!;
        await AgentSeed.AddQuotesAsync(TestUser.Sam, deck, "Small batches ship sooner.", "Measure what the customer sees.");

        var preview = await CallAsync(agent, "preview_deck", new() { ["deck"] = deck });

        var slides = preview.GetProperty("slides").EnumerateArray().Select(s => s.GetInt32()).ToList();
        slides.ShouldBe([1, 2]);
        var template = preview.GetProperty("render_url").GetString()!;
        template.ShouldContain("{slide}");
        var viewport = preview.GetProperty("viewport");
        var width = viewport.GetProperty("width").GetInt32();
        var height = viewport.GetProperty("height").GetInt32();
        var waitFor = preview.GetProperty("wait_for").GetString()!;
        var instructions = preview.GetProperty("instructions").GetString()!;
        await using var context = await Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        var pngs = new List<byte[]>();
        instructions.ShouldContain($"\"{template.Replace("{slide}", "$n")}\"");
        foreach (var slide in slides)
        {
            var url = template.Replace("{slide}", slide.ToString());
            var page = await context.NewPageAsync();
            await page.GotoAsync(url);
            await page.WaitForSelectorAsync(waitFor);
            (await page.GetByTestId("render-slide").GetAttributeAsync("data-error")).ShouldBeNull();
            new Uri(page.Url).Fragment.ShouldBeEmpty();
            pngs.Add(await page.ScreenshotAsync());
        }

        foreach (var png in pngs)
        {
            SizeOf(png).ShouldBe((1920, 1080));
        }
        pngs[0].SequenceEqual(pngs[1]).ShouldBeFalse("The two slides rendered the same PNG.");
    }

    private static async Task<JsonElement> CallAsync(McpClient agent, string tool, Dictionary<string, object?> args)
    {
        var result = await agent.CallToolAsync(tool, args);
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        (result.IsError ?? false).ShouldBeFalse($"{tool} failed: {text}");
        return JsonSerializer.Deserialize<JsonElement>(text);
    }

    // A PNG's width and height, from its IHDR chunk.
    private static (int Width, int Height) SizeOf(byte[] png)
        => (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
}
