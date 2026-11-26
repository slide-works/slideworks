using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ModelContextProtocol.Client;
using SlideWorks.AcceptanceTests.Journeys;
using SlideWorks.AcceptanceTests.Support;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.AcceptanceTests.Render;

// The PDF engine for real (P3): GET /api/decks/{id}/pdf through a signed-in browser context's request API drives the
// built SPA's /print page in headless Chromium and returns one page per slide. The server caches a deck's PDF, so every
// test makes a deck of its own. Decks go in the way an agent makes them, over MCP.
public class PdfRenderTests : AcceptanceTest
{
    [Test]
    public async Task GetPdf_WhenPresenterDownloadsADeck_ShouldReturnOnePagePerSlide()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CreateDeckAsync(agent, "Pages");
        await AddAsync(agent, deck, "quote", new { quote = "One change a day.", attribution = "Sam" });
        await AddAsync(agent, deck, "quote", new { quote = "Small batches win.", attribution = "Sam" });
        await AddAsync(agent, deck, "bullets", TenSlideBrief.Bullets);
        await AddAsync(agent, deck, "stack", new Dictionary<string, object>(TenSlideBrief.Stack) { ["reveal"] = "one-by-one" });
        await SignIn.AsAsync(Page, TestUser.Sam);

        var response = await Page.APIRequest.GetAsync($"/api/decks/{deck}/pdf");
        var pdf = await response.BodyAsync();

        response.Status.ShouldBe((int)HttpStatusCode.OK);
        PdfPages.IsPdf(pdf).ShouldBeTrue();
        PdfPages.Count(pdf).ShouldBe(4);
    }

    [Test]
    public async Task GetPdf_WhenTheDeckHasAnUploadedImage_ShouldStillRender()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CreateDeckAsync(agent, "Picture");
        var upload = await AgentSeed.CallAsync(agent, "upload_image", new() { ["deck"] = deck, ["name"] = "map.png", ["contentType"] = "image/png" });
        await PutImageAsync(upload.GetProperty("uploadUrl").GetString().ShouldNotBeNull(), SampleImage.Png());
        await AddAsync(agent, deck, "image", TenSlideBrief.Image(upload.GetProperty("image").GetString().ShouldNotBeNull()));
        await SignIn.AsAsync(Page, TestUser.Sam);

        var response = await Page.APIRequest.GetAsync($"/api/decks/{deck}/pdf");
        var pdf = await response.BodyAsync();

        response.Status.ShouldBe((int)HttpStatusCode.OK);
        PdfPages.IsPdf(pdf).ShouldBeTrue();
        PdfPages.Count(pdf).ShouldBe(1);
    }

    private static async Task<string> CreateDeckAsync(McpClient agent, string title)
    {
        var created = await AgentSeed.CallAsync(agent, "create_deck", new() { ["title"] = $"{title} {Guid.NewGuid():N}", ["template"] = "talk" });
        return created.GetProperty("deck").GetString().ShouldNotBeNull();
    }

    private static async Task AddAsync(McpClient agent, string deck, string layout, object slots)
    {
        var added = await AgentSeed.CallAsync(agent, "add_slide", new()
        {
            ["deck"] = deck,
            ["layout"] = layout,
            ["slots"] = JsonSerializer.SerializeToElement(slots),
        });
        added.GetProperty("warnings").GetArrayLength().ShouldBe(0);
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
