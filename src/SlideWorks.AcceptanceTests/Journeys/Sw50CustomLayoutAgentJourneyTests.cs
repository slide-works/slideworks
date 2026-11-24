using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-50: the agent's half of the custom-layout journey. On a published template that holds the five fixture layouts, an
// agent builds the test deck's five custom slides from the fixtures' own samples, turns the dial's needle by its value, and
// a person steps through a list that reveals one lane a click. The owner's half (upload, review, approve in the editor) is
// SW-488's; here the setup goes through the same routes a person's browser calls.
public class Sw50CustomLayoutAgentJourneyTests : AcceptanceTest
{
    private const double NEEDLE_TOLERANCE = 0.5;
    private const double BOX_TOLERANCE = 0.5;
    private const int CUSTOM_SLIDES = 5;
    private const string FRAMES = "() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)))";

    private static readonly string[] LAYOUTS = ["agent-lanes", "code-mock", "chat-mock", "dial", "horizon"];

    // The element under the slide whose class ends __needle (its full class holds the upload's asset id), with its rotation
    // read from the computed transform matrix and its drawn colour.
    private const string NEEDLE_PROBE =
        """
        slide => {
          const needle = slide.querySelector('[data-custom-layout="dial"] [class$="__needle"]');
          if (!needle) return null;
          const style = getComputedStyle(needle);
          const found = style.transform.match(/^matrix\(([^)]+)\)$/);
          if (!found) return null;
          const [a, b] = found[1].split(',').map(Number);
          const box = needle.getBoundingClientRect();
          const drawn = style.visibility === 'visible' && box.width > 0 && box.height > 0;
          return { angle: Math.atan2(b, a) * 180 / Math.PI, color: style.backgroundColor, drawn };
        }
        """;

    // How many script and img elements sit anywhere under the slide.
    private const string PICTURE_PROBE = "slide => slide.querySelectorAll('script, img').length";

    private const string TEXT_PROBE = "slide => slide.innerText.replace(/\\s+/g, ' ')";

    // The stepped elements of the slide (the layout's waiting list items): drawn visibility and the inline visibility the
    // element itself carries, and each one's box.
    private const string REVEAL_PROBE =
        """
        slide => [...slide.querySelectorAll('[data-custom-layout] [data-revealed]')].map((el) => {
          const box = el.getBoundingClientRect();
          const style = getComputedStyle(el);
          return {
            visible: style.visibility === 'visible' && style.opacity === '1',
            hidden: style.visibility === 'hidden',
            inline: el.style.visibility,
            left: box.left, top: box.top, width: box.width, height: box.height,
          };
        })
        """;

    [Test]
    public async Task Agent_WhenBuildingTheFiveCustomSlides_ShouldDrawEachWithoutAPicture()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await PublishedDeckAsync(agent, LAYOUTS);
        var samples = LAYOUTS.ToDictionary(name => name, SampleOf);

        var steps = new int[CUSTOM_SLIDES];
        for (var index = 0; index < CUSTOM_SLIDES; index++)
        {
            var added = await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deck, ["layout"] = LAYOUTS[index], ["slots"] = samples[LAYOUTS[index]] });
            added.GetProperty("slide").GetInt32().ShouldBe(index + 1);
            steps[index] = added.GetProperty("steps").GetInt32();
            added.GetProperty("warnings").GetArrayLength().ShouldBe(0, $"{LAYOUTS[index]}: {added.GetRawText()}");
        }

        // The companion for the picture probe: an image slide of the same deck, where the same probe must find its img.
        var upload = await AgentSeed.CallAsync(agent, "upload_image", new() { ["deck"] = deck, ["name"] = "cutout.png", ["contentType"] = "image/png" });
        await PutAsync(upload.GetProperty("uploadUrl").GetString().ShouldNotBeNull(), SampleImage.Png(), "image/png", HttpStatusCode.NoContent);
        var imageSlots = JsonSerializer.SerializeToElement(new
        {
            headline = "A picture for comparison",
            image = new { image = upload.GetProperty("image").GetString(), alt = "A square gradient." },
        });
        (await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deck, ["layout"] = "image", ["slots"] = imageSlots }))
            .GetProperty("slide").GetInt32().ShouldBe(CUSTOM_SLIDES + 1);

        var player = new PlayerPage(Page);
        for (var index = 0; index < CUSTOM_SLIDES; index++)
        {
            var number = index + 1;
            var name = LAYOUTS[index];
            // Opened at its last step, so every waiting item of a one-by-one slide is drawn when its text is read.
            await OpenAsync(player, deck, number, CUSTOM_SLIDES + 1, steps[index]);
            var slide = player.Slide(number);
            await Expect(slide.GetByTestId("custom-layout")).ToBeVisibleAsync();
            await Expect(slide.GetByTestId("custom-layout-error")).ToHaveCountAsync(0);
            await Page.EvaluateAsync(FRAMES);

            // Its texts are on the slide as drawn text (innerText leaves out the style sheet and hidden items).
            var text = await slide.EvaluateAsync<string>(TEXT_PROBE);
            foreach (var expected in TextsOf(name, samples[name]))
            {
                text.ShouldContain(Squash(expected), Case.Insensitive, customMessage: $"{name} should show \"{expected}\"");
            }

            // The built-in blocks the layouts hold: levels rungs and facts tiles, by their test ids.
            var rungs = LengthOf(samples[name], "levels", "items");
            var tiles = LengthOf(samples[name], "facts");
            await Expect(slide.GetByTestId("levels-rung").Filter(new() { Visible = true })).ToHaveCountAsync(rungs);
            await Expect(slide.GetByTestId("facts-tile").Filter(new() { Visible = true })).ToHaveCountAsync(tiles);

            // The overflow probe finds nothing outside the frame.
            var outside = await Eventually.ReadAsync(() => player.OverflowAsync(number), found => found.Length == 0);
            outside.ShouldBeEmpty($"{name} draws outside the frame");

            // No script or img under the custom slide; the page is shown to have caught up first (the count and the layout
            // are there) and the same probe finds the image slide's img below.
            await Expect(player.Count).ToHaveTextAsync($"{number} / {CUSTOM_SLIDES + 1}");
            (await slide.EvaluateAsync<int>(PICTURE_PROBE)).ShouldBe(0, $"{name} must draw without a picture");
        }

        await OpenAsync(player, deck, CUSTOM_SLIDES + 1, CUSTOM_SLIDES + 1);
        await player.ImageLoadedAsync(CUSTOM_SLIDES + 1);
        (await player.Slide(CUSTOM_SLIDES + 1).EvaluateAsync<int>(PICTURE_PROBE)).ShouldBe(1, "the same probe finds the image slide's img");

        // The deck page: six thumbnails, the five custom ones drawn and none in an error state.
        var deckPage = new DeckPage(Page);
        await deckPage.GotoAsync(deck);
        await Expect(deckPage.Thumbnails).ToHaveCountAsync(CUSTOM_SLIDES + 1);
        for (var number = 1; number <= CUSTOM_SLIDES; number++)
        {
            await Expect(deckPage.Thumbnail(number).GetByTestId("custom-layout").Filter(new() { Visible = true })).ToHaveCountAsync(1);
        }
        await Page.EvaluateAsync(FRAMES);
        for (var number = 1; number <= CUSTOM_SLIDES; number++)
        {
            await Expect(deckPage.Thumbnail(number).GetByTestId("custom-layout-error")).ToHaveCountAsync(0);
        }
    }

    [Test]
    public async Task Agent_WhenChangingTheDialsValue_ShouldTurnTheNeedle()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await PublishedDeckAsync(agent, ["dial"]);
        var sample = SampleOf("dial");
        var headline = sample.GetProperty("headline").GetString().ShouldNotBeNull();
        sample.GetProperty("value").GetInt32().ShouldBe(30);

        var first = await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deck, ["layout"] = "dial", ["slots"] = sample });
        first.GetProperty("warnings").GetArrayLength().ShouldBe(0);

        // A value past the dial's limit is refused with its path, and the deck keeps its one slide.
        var tooFar = JsonNode.Parse(sample.GetRawText())!;
        tooFar["value"] = 140;
        var before = await SlideCountAsync(agent, deck);
        await using var raw = await AgentSeed.ConnectAsync(TestUser.Sam);
        var refused = await raw.CallToolAsync("add_slide", new Dictionary<string, object?>
        {
            ["deck"] = deck,
            ["layout"] = "dial",
            ["slots"] = JsonSerializer.SerializeToElement(tooFar),
        });
        (refused.IsError ?? false).ShouldBeTrue("a dial value of 140 should be refused");
        string.Concat(refused.Content.OfType<TextContentBlock>().Select(b => b.Text)).ShouldContain("dial.value");
        before.ShouldBe(1);
        (await SlideCountAsync(agent, deck)).ShouldBe(before);

        // The player at 30: the visible needle points at -36 degrees, and the headline and both end labels are drawn text.
        var player = new PlayerPage(Page);
        await OpenAsync(player, deck, 1, 1);
        await Expect(player.Slide(1).GetByTestId("custom-layout")).ToBeVisibleAsync();
        var atThirtyNeedle = await ReadNeedleAsync(player, 1, -36);
        atThirtyNeedle.ShouldNotBeNull("the dial's needle is not there").Drawn.ShouldBeTrue("the needle at 30 is not visible");
        atThirtyNeedle.Angle.ShouldBe(-36, NEEDLE_TOLERANCE);
        var ends = new[] { headline, sample.GetProperty("from").GetString()!, sample.GetProperty("to").GetString()! };
        var text = await player.Slide(1).EvaluateAsync<string>(TEXT_PROBE);
        foreach (var expected in ends)
        {
            text.ShouldContain(Squash(expected), Case.Insensitive);
        }

        // The preview at 30, then the value moves to 85.
        var atThirty = await PreviewAsync(agent, deck);
        var turned = JsonNode.Parse(sample.GetRawText())!;
        turned["value"] = 85;
        var updated = await AgentSeed.CallAsync(agent, "update_slide", new()
        {
            ["deck"] = deck,
            ["slide"] = 1,
            ["revision"] = first.GetProperty("revision").GetInt64(),
            ["slots"] = JsonSerializer.SerializeToElement(turned),
        });
        updated.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        var atEightyFive = await PreviewAsync(agent, deck);
        atThirty.SequenceEqual(atEightyFive).ShouldBeFalse("the dial at 30 and at 85 rendered the same PNG");

        // The same slide, reloaded: the needle has turned to 63 degrees.
        await OpenAsync(player, deck, 1, 1);
        var turnedNeedle = await ReadNeedleAsync(player, 1, 63);
        turnedNeedle.ShouldNotBeNull("the dial's needle is not there").Drawn.ShouldBeTrue("the needle at 85 is not visible");
        turnedNeedle.Angle.ShouldBe(63, NEEDLE_TOLERANCE);
        text = await player.Slide(1).EvaluateAsync<string>(TEXT_PROBE);
        foreach (var expected in ends)
        {
            text.ShouldContain(Squash(expected), Case.Insensitive);
        }

        // A second dial on the dark surface, opened as the active slide: same value as the first (30), its own needle colour.
        var dark = await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deck, ["layout"] = "dial", ["slots"] = sample, ["surface"] = "dark" });
        dark.GetProperty("slide").GetInt32().ShouldBe(2);
        dark.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        await OpenAsync(player, deck, 2, 2);
        await Expect(player.Slide(2).GetByTestId("custom-layout")).ToBeVisibleAsync();
        (await new SlideProbe(Page, 1).SurfaceAsync()).ShouldNotBe("dark");
        (await new SlideProbe(Page, 2).SurfaceAsync()).ShouldBe("dark");
        var darkNeedle = await ReadNeedleAsync(player, 2, -36);
        darkNeedle.ShouldNotBeNull("the dark dial's needle is not there").Drawn.ShouldBeTrue("the dark needle is not visible");
        darkNeedle.Angle.ShouldBe(-36, NEEDLE_TOLERANCE);
        darkNeedle.Color.ShouldNotBe(turnedNeedle.Color, "the needle should be drawn in another colour on the dark surface");
    }

    [Test]
    public async Task Person_WhenSteppingThroughAgentLanes_ShouldRevealOneLaneAClick()
    {
        await SignIn.AsAsync(Page, TestUser.Sam);
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await PublishedDeckAsync(agent, ["agent-lanes"]);
        var sample = SampleOf("agent-lanes");
        var lanes = sample.GetProperty("lanes").GetArrayLength();
        var added = await AgentSeed.CallAsync(agent, "add_slide", new() { ["deck"] = deck, ["layout"] = "agent-lanes", ["slots"] = sample });
        added.GetProperty("steps").GetInt32().ShouldBe(lanes);

        var player = new PlayerPage(Page);
        await OpenAsync(player, deck, 1, 1);
        var steps = Page.GetByTestId("player-steps");
        await Expect(steps).ToBeVisibleAsync();
        await Expect(steps).ToHaveAttributeAsync("aria-label", $"Step 0 of {lanes}");
        (await steps.EvaluateAsync<int>("el => el.childElementCount")).ShouldBe(lanes);
        var slide = player.Slide(1);

        var waiting = await RevealAsync(slide);
        waiting.Count.ShouldBe(lanes);
        waiting.ShouldAllBe(lane => !lane.Visible && lane.Hidden && lane.Inline == "hidden");

        await StepAsync(steps, 1, lanes);
        var one = await RevealAsync(slide);
        one.Count.ShouldBe(lanes);
        one[0].Visible.ShouldBeTrue("the first lane shows after one click");
        one[1].Hidden.ShouldBeTrue("the second lane still waits");
        one[1].Inline.ShouldBe("hidden");
        one.Skip(2).ShouldAllBe(lane => lane.Hidden && lane.Inline == "hidden", "lanes after the second still wait");

        for (var step = 2; step <= lanes; step++)
        {
            await StepAsync(steps, step, lanes);
        }
        var all = await RevealAsync(slide);
        all.Count.ShouldBe(lanes);
        all.ShouldAllBe(lane => lane.Visible);

        // A waiting lane keeps the box it has once shown.
        one[1].Width.ShouldBeGreaterThan(0);
        one[1].IsSameBoxAs(all[1]).ShouldBeTrue($"the waiting lane's box {one[1]} should match its shown box {all[1]}");
        one[0].IsSameBoxAs(all[0]).ShouldBeTrue($"the first lane's box {one[0]} should not move: {all[0]}");
    }

    // A template from talk with the named fixtures uploaded, listed, approved and published, and an empty deck on it.
    private async Task<string> PublishedDeckAsync(McpClient agent, string[] names)
    {
        var headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl };
        var created = await Page.APIRequest.PostAsync("/api/templates", new() { Headers = headers, DataObject = new { basedOn = "talk", name = $"Northwind {Guid.NewGuid():N}"[..28] } });
        created.Status.ShouldBe(201, await created.TextAsync());
        var template = JsonDocument.Parse(await created.TextAsync()).RootElement.GetProperty("id").GetString().ShouldNotBeNull();

        var assets = new Dictionary<string, string>();
        foreach (var name in names)
        {
            assets[name] = await UploadAsync(template, name, headers);
        }

        var editorResponse = await Page.APIRequest.GetAsync($"/api/templates/{template}");
        editorResponse.Status.ShouldBe(200, await editorResponse.TextAsync());
        var editor = JsonDocument.Parse(await editorResponse.TextAsync()).RootElement;
        var manifest = JsonNode.Parse(editor.GetProperty("manifest").GetRawText())!;
        manifest["customLayouts"] = new JsonArray([.. names.Select(name => (JsonNode)new JsonObject { ["name"] = name, ["file"] = assets[name] })]);
        var saved = await Page.APIRequest.PutAsync($"/api/templates/{template}", new() { Headers = headers, DataObject = new { manifest, revision = editor.GetProperty("revision").GetInt32() } });
        saved.Status.ShouldBe(200, await saved.TextAsync());

        foreach (var name in names)
        {
            var approved = await Page.APIRequest.PostAsync(
                $"/api/templates/{template}/layouts/{name}/approve",
                new() { Headers = headers, DataObject = new { asset = assets[name], overflow = Array.Empty<string>(), contrast = Array.Empty<string>() } });
            approved.Status.ShouldBe(204, await approved.TextAsync());
        }
        var published = await Page.APIRequest.PostAsync($"/api/templates/{template}/publish", new() { Headers = headers });
        published.Status.ShouldBe(200, await published.TextAsync());

        var made = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"Custom {Guid.NewGuid():N}"[..20], ["template"] = template });
        return made.GetProperty("deck").GetString().ShouldNotBeNull();
    }

    // The fixture as one {markup, css, slots} document, uploaded as a layout; returns the asset id.
    private async Task<string> UploadAsync(string template, string name, Dictionary<string, string> headers)
    {
        var folder = FolderOf(name);
        var body = new JsonObject
        {
            ["markup"] = await File.ReadAllTextAsync(Path.Combine(folder, "layout.html")),
            ["css"] = await File.ReadAllTextAsync(Path.Combine(folder, "layout.css")),
            ["slots"] = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "slots.json"))),
        }.ToJsonString();
        var start = await Page.APIRequest.PostAsync($"/api/templates/{template}/assets", new() { Headers = headers, DataObject = new { kind = "layout", fileName = "layout.json", rightsConfirmed = false } });
        start.Status.ShouldBe(201, await start.TextAsync());
        var url = JsonDocument.Parse(await start.TextAsync()).RootElement.GetProperty("uploadUrl").GetString().ShouldNotBeNull();
        var stored = await PutAsync(Absolute(url), System.Text.Encoding.UTF8.GetBytes(body), "application/json", HttpStatusCode.OK);
        return JsonDocument.Parse(stored).RootElement.GetProperty("assetId").GetString().ShouldNotBeNull();
    }

    private static string Absolute(string url) => url.StartsWith('/') ? AcceptanceApp.BaseUrl.TrimEnd('/') + url : url;

    private static string FolderOf(string name)
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideWorks.slnx")))
        {
            directory = directory.Parent;
        }
        return Path.Combine(directory.ShouldNotBeNull().FullName, "SlideWorks.WebApi.Tests", "Templates", "Fixtures", "custom-layouts", name);
    }

    private static JsonElement SampleOf(string name) =>
        JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(FolderOf(name), "sample.json")));

    // The slot texts a sample puts on its slide, each written as the path to read it by ("lanes[].task").
    private static IEnumerable<string> TextsOf(string name, JsonElement sample)
    {
        string[] paths = name switch
        {
            "agent-lanes" => ["kicker", "headline", "lead", "lanes[].who", "lanes[].task", "lanes[].status", "levels.title", "levels.items[].text"],
            "code-mock" => ["kicker", "headline", "file", "hint", "lines[].code", "lines[].ghost", "facts[].label", "facts[].value", "levels.title", "levels.items[].text"],
            "chat-mock" => ["kicker", "headline", "messages[].text", "messages[].code", "hint", "facts[].label", "facts[].value", "levels.title", "levels.items[].text"],
            "dial" => ["kicker", "headline", "label", "from", "to", "text"],
            _ => ["kicker", "headline", "rows[].label", "rows[].sub", "rows[].segments[].text", "ticks[]"],
        };
        return paths.SelectMany(path => Read(sample, path.Split('.'))).Select(text => text.Trim()).Where(text => text.Length > 0);
    }

    private static IEnumerable<string> Read(JsonElement node, string[] path)
    {
        if (path.Length == 0)
        {
            if (node.ValueKind == JsonValueKind.String)
            {
                yield return node.GetString()!;
            }
            yield break;
        }
        var part = path[0];
        var each = part.EndsWith("[]", StringComparison.Ordinal);
        var key = each ? part[..^2] : part;
        if (key.Length > 0 && (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(key, out node)))
        {
            yield break;
        }
        var items = each ? node.EnumerateArray().ToList() : [node];
        foreach (var item in items)
        {
            foreach (var found in Read(item, path[1..]))
            {
                yield return found;
            }
        }
    }

    // The length of the array at the path in a sample; 0 where the sample has none.
    private static int LengthOf(JsonElement sample, params string[] path)
    {
        var node = sample;
        foreach (var key in path)
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(key, out node))
            {
                return 0;
            }
        }
        return node.ValueKind == JsonValueKind.Array ? node.GetArrayLength() : 0;
    }

    private static string Squash(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private async Task OpenAsync(PlayerPage player, string deck, int number, int total, int step = 0)
    {
        await player.GotoAsync(deck, hash: step == 0 ? $"#{number}" : $"#{number}.{step}");
        await Page.ReloadAsync();
        await Expect(player.Player).ToBeVisibleAsync();
        await Expect(player.Count).ToHaveTextAsync($"{number} / {total}");
        await Expect(player.Layer(number)).ToHaveAttributeAsync("data-active", "true");
        await player.FontsReadyAsync();
    }

    private static async Task<int> SlideCountAsync(McpClient agent, string deck) =>
        (await AgentSeed.CallAsync(agent, "get_deck", new() { ["deck"] = deck })).GetProperty("slides").GetArrayLength();

    // preview_deck, then the screenshot of its one slide the way its instructions say: 1920 x 1080.
    private async Task<byte[]> PreviewAsync(McpClient agent, string deck)
    {
        var preview = await AgentSeed.CallAsync(agent, "preview_deck", new() { ["deck"] = deck, ["slides"] = new[] { 1 } });
        var pngs = await AgentSeed.ScreenshotPreviewAsync(Browser, preview);
        var png = pngs.Single().Value.Png;
        AgentSeed.PngSize(png).ShouldBe((1920, 1080));
        return png;
    }

    private static async Task<Needle?> ReadNeedleAsync(PlayerPage player, int number, double expected)
    {
        var slide = player.Slide(number);
        return await Eventually.ReadAsync(
            async () =>
            {
                var found = await slide.EvaluateAsync<JsonElement>(NEEDLE_PROBE);
                return found.ValueKind == JsonValueKind.Null
                    ? null
                    : new Needle(found.GetProperty("angle").GetDouble(), found.GetProperty("color").GetString()!, found.GetProperty("drawn").GetBoolean());
            },
            needle => needle is not null && Math.Abs(needle.Angle - expected) <= NEEDLE_TOLERANCE);
    }

    private async Task StepAsync(ILocator steps, int step, int total)
    {
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(steps).ToHaveAttributeAsync("aria-label", $"Step {step} of {total}");
        await Page.EvaluateAsync(FRAMES);
    }

    private static async Task<IReadOnlyList<Lane>> RevealAsync(ILocator slide)
    {
        var found = await slide.EvaluateAsync<JsonElement>(REVEAL_PROBE);
        return [.. found.EnumerateArray().Select(l => new Lane(
            l.GetProperty("visible").GetBoolean(),
            l.GetProperty("hidden").GetBoolean(),
            l.GetProperty("inline").GetString()!,
            l.GetProperty("left").GetDouble(),
            l.GetProperty("top").GetDouble(),
            l.GetProperty("width").GetDouble(),
            l.GetProperty("height").GetDouble()))];
    }

    private static async Task<string> PutAsync(string url, byte[] bytes, string contentType, HttpStatusCode expected)
    {
        using var http = new HttpClient();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        using var response = await http.PutAsync(url, content);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, body);
        return body;
    }

    private sealed record Needle(double Angle, string Color, bool Drawn);

    private sealed record Lane(bool Visible, bool Hidden, string Inline, double Left, double Top, double Width, double Height)
    {
        public bool IsSameBoxAs(Lane other) =>
            Math.Abs(Left - other.Left) <= BOX_TOLERANCE && Math.Abs(Top - other.Top) <= BOX_TOLERANCE
            && Math.Abs(Width - other.Width) <= BOX_TOLERANCE && Math.Abs(Height - other.Height) <= BOX_TOLERANCE;
    }
}
