using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Journeys.Rebuild;

// The setup both halves of the rebuild check share (SW-490, SW-491): an agent over MCP makes a template from talk with the
// normal-width test font for head and body and the top bar on accent, a person confirms the font rights (an agent cannot),
// the agent publishes it and makes a deck of seven parts and a marked appendix on it.
public static class RebuildDeck
{
    public const string FAMILY = "Northwind Sans";

    // Seven made-up parts and the appendix, each at the documented maximum (name 40, short 16, caption 24), plain generated
    // words with no storyline. The appendix is the last part and carries the mark.
    public static readonly string[] PART_NAMES = [.. Enumerable.Range(0, 7).Select(i => MadeUpWords.Of(40, 200 + i * 3))];

    public static IReadOnlyList<object> Parts =>
    [
        .. PART_NAMES.Select((name, i) => new { name, @short = MadeUpWords.Of(16, 201 + i * 3), caption = MadeUpWords.Of(24, 202 + i * 3), mark = (string?)null }),
        new { name = MadeUpWords.Of(40, 230), @short = MadeUpWords.Of(16, 231), caption = MadeUpWords.Of(24, 232), mark = (string?)"A" },
    ];

    // Calls a tool and returns its text and whether it was refused, for a caller that records a refusal's exact message.
    public static async Task<(bool Refused, string Text)> TryAsync(McpClient agent, string tool, Dictionary<string, object?> args)
    {
        var result = await agent.CallToolAsync(tool, args);
        return (result.IsError ?? false, string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text)));
    }

    // Makes and publishes the template, makes the deck on it, and returns the deck's id. The page must be signed in as Sam.
    public static async Task<string> MakeAsync(McpClient agent, IPage signedInAsSam, string name, string title)
    {
        var created = await AgentSeed.CallAsync(agent, "create_template", new() { ["name"] = name, ["from"] = "talk" });
        var id = created.GetProperty("template").GetString()!;

        var upload = await AgentSeed.CallAsync(agent, "upload_asset", new() { ["template"] = id, ["kind"] = "font", ["filename"] = "normal.woff2" });
        var stored = await PutAsync(upload.GetProperty("upload_url").GetString()!, FontFixtures.Normal(), "font/woff2", HttpStatusCode.OK);
        var assetId = JsonDocument.Parse(stored).RootElement.GetProperty("assetId").GetString()!;

        // "type" and "chrome" replace as a whole: start from the copy's own and change only what is ours.
        var current = (await AgentSeed.CallAsync(agent, "get_template", new() { ["template"] = id })).GetProperty("manifest");
        var type = JsonSerializer.Deserialize<Dictionary<string, object?>>(current.GetProperty("type").GetRawText())!;
        type["head"] = FAMILY;
        type["body"] = FAMILY;
        var chrome = JsonSerializer.Deserialize<Dictionary<string, object?>>(current.GetProperty("chrome").GetRawText())!;
        chrome["topBar"] = "accent";
        var fonts = new[]
        {
            new { role = "head", family = FAMILY, file = assetId, weight = 400, style = "normal" },
            new { role = "body", family = FAMILY, file = assetId, weight = 400, style = "normal" },
        };
        await AgentSeed.CallAsync(agent, "update_template", new() { ["template"] = id, ["manifest"] = JsonSerializer.SerializeToElement(new { fonts, type, chrome }) });

        var tick = await signedInAsSam.APIRequest.PostAsync($"/api/templates/{id}/assets/font-rights", new() { Headers = new Dictionary<string, string> { ["Origin"] = AcceptanceApp.BaseUrl } });
        tick.Status.ShouldBe(200, await tick.TextAsync());
        (await AgentSeed.CallAsync(agent, "publish_template", new() { ["template"] = id })).GetProperty("version").GetInt32().ShouldBe(1);

        var deck = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = title, ["template"] = id, ["parts"] = JsonSerializer.SerializeToElement(Parts) });
        return deck.GetProperty("deck").GetString()!;
    }

    // Uploads a test image to the deck the way an agent does (a one-time URL, a PUT with no auth header) and returns its id.
    public static async Task<string> UploadImageAsync(McpClient agent, string deck)
    {
        var upload = await AgentSeed.CallAsync(agent, "upload_image", new() { ["deck"] = deck, ["name"] = "screen.png", ["contentType"] = "image/png" });
        await PutAsync(upload.GetProperty("uploadUrl").GetString()!, PngImage.Gradient(640, 360), "image/png", HttpStatusCode.NoContent);
        return upload.GetProperty("image").GetString()!;
    }

    private static async Task<string> PutAsync(string uploadUrl, byte[] bytes, string contentType, HttpStatusCode expected)
    {
        using var http = new HttpClient();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await http.PutAsync(uploadUrl, content);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(expected, body);
        return body;
    }
}
