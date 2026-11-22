using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys;

// SW-50: an agent with only the MCP URL makes a template with its own font and builds a deck on it. A person confirms
// the right to serve the font (an agent cannot), and a line that is too long in that font is refused on the template
// while the same line is accepted on the built-in it was copied from. Everything but the font-rights tick is MCP.
public class Sw50AgentTemplateJourneyTests : AcceptanceTest
{
    private const string FAMILY = "Agent Wide Sans";
    private const int MAX_TRIES = 60;
    private const double MIN_WIDTH_EM = 0.57;
    private const string HEAD_FONT = "el => getComputedStyle(el).fontFamily";
    private const string STAGE_TEXT = "el => el.textContent";
    private const string FACE_LOADED = """
        family => [...document.fonts].some(f => f.family.replace(/["']/g, '') === family && f.status === 'loaded')
            && document.fonts.check(`16px "${family}"`)
        """;

    private static readonly string[] TEMPLATE_TOOLS = ["create_template", "get_template", "list_templates", "publish_template", "update_template", "upload_asset"];

    [Test]
    public async Task Agent_WhenUploadingAFontAndPublishing_ShouldGetFitRefusalsInThatFont()
    {
        var name = $"Agent Studio {Guid.NewGuid():N}"[..28];
        using var oauth = await AgentOAuthClient.RegisterAsync(AcceptanceApp.BaseUrl, "Claude");
        await using var agent = await AgentSession.ConnectAsync(oauth, Page, TestUser.Sam);
        await using var raw = await AgentSeed.ConnectAsync(TestUser.Sam);
        (await agent.ToolNamesAsync()).Intersect(TEMPLATE_TOOLS).Order(StringComparer.Ordinal).ShouldBe(TEMPLATE_TOOLS);

        var created = await agent.CallAsync("create_template", new { name, from = "talk" });
        var id = created.GetProperty("template").GetString().ShouldNotBeNull();
        id.ShouldStartWith("tpl_");
        created.GetProperty("status").GetString().ShouldBe("draft");
        var editorUrl = created.GetProperty("url").GetString().ShouldNotBeNull();
        editorUrl.ShouldBe($"{AcceptanceApp.BaseUrl}/templates/{id}/edit");

        // A file that is not a font is refused and nothing is stored.
        var badUpload = await agent.CallAsync("upload_asset", new { template = id, kind = "font", filename = "broken.woff2" });
        badUpload.GetProperty("kind").GetString().ShouldBe("font");
        var refused = await PutAsync(badUpload.GetProperty("upload_url").GetString().ShouldNotBeNull(), FontFixtures.NotAFont());
        ((int)refused.Status).ShouldBeInRange(400, 499, refused.Body);
        refused.Body.ShouldContain("Fonts must be woff2 files");
        (await FilesAsync(agent, id)).ShouldBeEmpty();

        // The wide font is accepted: measured wide enough to part from the built-in, and stored with the rights unconfirmed.
        var upload = await agent.CallAsync("upload_asset", new { template = id, kind = "font", filename = "wide.woff2" });
        upload.GetProperty("kind").GetString().ShouldBe("font");
        var stored = await PutAsync(upload.GetProperty("upload_url").GetString().ShouldNotBeNull(), FontFixtures.Wide());
        stored.Status.ShouldBe(HttpStatusCode.OK, stored.Body);
        var json = JsonDocument.Parse(stored.Body).RootElement;
        var assetId = json.GetProperty("assetId").GetString().ShouldNotBeNull();
        json.GetProperty("kind").GetString().ShouldBe("font");
        var width = json.GetProperty("meta").GetProperty("avgCharEm").GetDouble();
        width.ShouldBeGreaterThanOrEqualTo(MIN_WIDTH_EM, "the wide fixture must measure wider than the built-in body font (0.56) for this journey to mean anything");

        var fonts = new[]
        {
            new { role = "head", family = FAMILY, file = assetId, weight = 900, style = "normal" },
            new { role = "body", family = FAMILY, file = assetId, weight = 900, style = "normal" },
        };
        // "type" replaces as a whole, so start from the copy's own and change only the families.
        var copied = (await agent.CallAsync("get_template", new { template = id })).GetProperty("manifest").GetProperty("type");
        var type = JsonSerializer.Deserialize<Dictionary<string, object?>>(copied.GetRawText())!;
        type["head"] = FAMILY;
        type["body"] = FAMILY;
        var updated = await agent.CallAsync("update_template", new { template = id, manifest = new { fonts, type } });
        updated.GetProperty("revision").GetInt32().ShouldBeGreaterThan(created.GetProperty("revision").GetInt32());
        var read = await agent.CallAsync("get_template", new { template = id });
        var files = read.GetProperty("files").EnumerateArray().ToList();
        files.Count.ShouldBe(1);
        files[0].GetProperty("asset").GetString().ShouldBe(assetId);
        files[0].GetProperty("kind").GetString().ShouldBe("font");
        files[0].GetProperty("rightsConfirmed").GetBoolean().ShouldBeFalse();
        read.GetProperty("manifest").GetProperty("type").GetProperty("head").GetString().ShouldBe(FAMILY);
        read.GetProperty("manifest").GetProperty("type").GetProperty("body").GetString().ShouldBe(FAMILY);
        read.GetProperty("manifest").GetProperty("fonts").EnumerateArray().Select(f => f.GetProperty("file").GetString()).ShouldBe([assetId, assetId]);

        // Publish is refused until a person confirms the right to serve the font; the template is not listed.
        var cannot = await ErrorOfAsync(raw, "publish_template", new() { ["template"] = id });
        cannot.ShouldStartWith("Can't publish yet: ");
        cannot.ShouldContain("Confirm you can serve these fonts");
        cannot.ShouldContain($"Ask a person to open {editorUrl}");
        (await ListedAsync(agent, id)).ShouldBeEmpty();
        (await ListedAsync(agent, "talk")).Count.ShouldBe(1, "the same probe finds a template that is listed");
        (await agent.CallAsync("get_template", new { template = id })).GetProperty("status").GetString().ShouldBe("draft");

        // Sam ticks the box (the one step an agent cannot take); publishing now gives version 1.
        var tick = await Page.APIRequest.PostAsync($"/api/templates/{id}/assets/font-rights", new() { Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl } });
        tick.Status.ShouldBe(200, await tick.TextAsync());
        JsonDocument.Parse(await tick.TextAsync()).RootElement.GetProperty("confirmed").GetInt32().ShouldBe(1);
        (await FilesAsync(agent, id)).Single().GetProperty("rightsConfirmed").GetBoolean().ShouldBeTrue();
        var published = await agent.CallAsync("publish_template", new { template = id });
        published.GetProperty("version").GetInt32().ShouldBe(1);
        var listed = (await ListedAsync(agent, id)).Single();
        listed.GetProperty("version").GetInt32().ShouldBe(1);
        listed.GetProperty("source").GetString().ShouldBe("yours");

        // Two decks, one on talk and one on the template; find a line the built-in takes and the template refuses.
        var onTalk = (await agent.CallAsync("create_deck", new { title = "Fit on talk", template = "talk" })).GetProperty("deck").GetString().ShouldNotBeNull();
        var createdDeck = await agent.CallAsync("create_deck", new { title = "Fit on the template", template = id });
        var deck = createdDeck.GetProperty("deck").GetString().ShouldNotBeNull();
        createdDeck.GetProperty("look").GetProperty("template").GetString().ShouldBe(id);
        createdDeck.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(1);

        var (words, onTemplateAnswer) = await FindPartingLineAsync(raw, onTalk, deck);
        onTemplateAnswer.ShouldContain("items");
        onTemplateAnswer.ShouldStartWith("bullets.items: too much text to fit on the slide");
        (await agent.CallAsync("get_deck", new { deck })).GetProperty("slides").GetArrayLength().ShouldBe(0);

        var accepted = await agent.CallAsync("add_slide", new { deck, layout = "quote", slots = new { quote = "Wide letters, short lines.", attribution = "Sam" } });
        accepted.GetProperty("slide").GetInt32().ShouldBe(1);
        var built = await agent.CallAsync("get_deck", new { deck });
        built.GetProperty("slides").GetArrayLength().ShouldBe(1);
        built.GetProperty("look").GetProperty("template").GetString().ShouldBe(id);
        built.GetProperty("look").GetProperty("templateVersion").GetInt32().ShouldBe(1);

        var player = new PlayerPage(Page);
        await player.GotoAsync(deck);
        await Expect(player.Slide(1)).ToBeVisibleAsync();
        await Expect(player.Count).ToContainTextAsync("/ 1");
        await player.FontsReadyAsync();
        var head = await Page.GetByTestId("quote-text").First.EvaluateAsync<string>(HEAD_FONT);
        head.Trim().Replace("\"", "'").ShouldStartWith($"'{FAMILY}'");
        (await Eventually.ReadAsync(() => Page.EvaluateAsync<bool>(FACE_LOADED, FAMILY), ok => ok)).ShouldBeTrue();
        var stageText = await player.Stage.EvaluateAsync<string>(STAGE_TEXT);
        stageText.ShouldContain("Wide letters, short lines.");
        stageText.ShouldNotContain(Marker);

        // The deck's activity names the template by its label, read back from get_template.
        var label = read.GetProperty("name").GetString().ShouldNotBeNull();
        await Page.GotoAsync(DeckPage.PathFor(deck));
        var deckPage = new DeckPage(Page);
        await Expect(deckPage.ActivityRows.Filter(new LocatorFilterOptions { HasText = "created the deck" }))
            .ToContainTextAsync($"from the {label} template with the");
        words.ShouldBeGreaterThan(0);
    }

    private const string Marker = "Refusedmarker";

    // Sends a bullets line one word longer at a time to both decks until talk accepts it and the template refuses it.
    private static async Task<(int Step, string TemplateAnswer)> FindPartingLineAsync(McpClient raw, string onTalk, string onTemplate)
    {
        (bool Error, string Text) lastTalk = default, lastTemplate = default;
        for (var step = 1; step <= MAX_TRIES; step++)
        {
            var slots = new Dictionary<string, object?> { ["headline"] = Marker, ["items"] = Items(step) };
            lastTalk = await AddAsync(raw, onTalk, slots);
            if (lastTalk.Error)
            {
                // talk refused it first: the line is already too long for both. Nothing to learn from later steps.
                break;
            }
            lastTemplate = await AddAsync(raw, onTemplate, slots);
            if (lastTemplate.Error)
            {
                return (step, lastTemplate.Text);
            }
            // Both took it: remove the slide again so the template deck stays empty for the next try.
            await AgentSeed.CallAsync(raw, "delete_slide", new() { ["deck"] = onTemplate, ["slide"] = 1 });
            await AgentSeed.CallAsync(raw, "delete_slide", new() { ["deck"] = onTalk, ["slide"] = 1 });
        }
        throw new ShouldAssertException($"No line was accepted on talk and refused on the template within {MAX_TRIES} tries. talk: {lastTalk}; template: {lastTemplate}");
    }

    // Four points of plain five-letter words. Each step adds one word to one point in turn, so the line grows one word at a time.
    private static string[] Items(int step) =>
        [.. Enumerable.Range(0, 4).Select(k => string.Join(' ', Enumerable.Repeat("alpha", 20 + (step / 4) + (k < step % 4 ? 1 : 0))))];

    private static async Task<(bool Error, string Text)> AddAsync(McpClient raw, string deck, Dictionary<string, object?> slots)
    {
        var args = new Dictionary<string, object?> { ["deck"] = deck, ["layout"] = "bullets", ["slots"] = JsonSerializer.SerializeToElement(slots) };
        var result = await raw.CallToolAsync("add_slide", args);
        return (result.IsError ?? false, string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text)));
    }

    private static async Task<string> ErrorOfAsync(McpClient raw, string tool, Dictionary<string, object?> args)
    {
        var result = await raw.CallToolAsync(tool, args);
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
        (result.IsError ?? false).ShouldBeTrue($"{tool} should have been refused: {text}");
        return text;
    }

    private static async Task<List<JsonElement>> FilesAsync(AgentSession agent, string id) =>
        [.. (await agent.CallAsync("get_template", new { template = id })).GetProperty("files").EnumerateArray()];

    private static async Task<List<JsonElement>> ListedAsync(AgentSession agent, string reference) =>
        [.. (await agent.CallAsync("list_templates", new { })).GetProperty("templates").EnumerateArray().Where(t => t.GetProperty("ref").GetString() == reference)];

    // The agent PUTs the raw bytes to the one-time upload URL with no auth header, as upload_asset's curl line does.
    private static async Task<(HttpStatusCode Status, string Body)> PutAsync(string uploadUrl, byte[] bytes)
    {
        using var http = new HttpClient();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("font/woff2");
        using var response = await http.PutAsync(uploadUrl, content);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
