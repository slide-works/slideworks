using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using SlideWorks.Core.Rendering;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Rendering;
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

    [Test]
    public async Task GetPdf_WhenTheRenderPageReportsAnError_ShouldBe503WithTheReason()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CreateDeckAsync(agent, "Broken picture");
        var upload = await AgentSeed.CallAsync(agent, "upload_image", new() { ["deck"] = deck, ["name"] = "map.png", ["contentType"] = "image/png" });
        await PutImageAsync(upload.GetProperty("uploadUrl").GetString().ShouldNotBeNull(), SampleImage.Png());
        var image = upload.GetProperty("image").GetString().ShouldNotBeNull();
        await AddAsync(agent, deck, "image", TenSlideBrief.Image(image));
        File.Delete(AcceptanceApp.Services.GetRequiredService<ImageStore>().PathFor(deck, image));
        await SignIn.AsAsync(Page, TestUser.Sam);

        var response = await Page.APIRequest.GetAsync($"/api/decks/{deck}/pdf");
        var body = await response.TextAsync();

        response.Status.ShouldBe((int)HttpStatusCode.ServiceUnavailable);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("error").GetString().ShouldBe($"Couldn't make the PDF: image {image}");
        body.ShouldNotContain("token=");
    }

    [Test]
    public async Task Engine_PdfRenderTimesOut()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CreateDeckAsync(agent, "Slow");
        await AddAsync(agent, deck, "quote", new { quote = "One change a day.", attribution = "Sam" });
        await using var browser = new RenderBrowser();
        using var engine = EngineWith(browser, TimeSpan.FromMilliseconds(1), AcceptanceApp.BaseUrl);

        var error = await Should.ThrowAsync<TimeoutException>(() => engine.RenderPdfAsync(deck, CancellationToken.None));

        error.Message.ShouldBe(PlaywrightRenderEngine.TIMED_OUT);
    }

    [Test]
    public async Task Engine_PdfRenderFailureCarriesNoToken()
    {
        await using var agent = await AgentSeed.ConnectAsync(TestUser.Sam);
        var deck = await CreateDeckAsync(agent, "Unreachable");
        await AddAsync(agent, deck, "quote", new { quote = "One change a day.", attribution = "Sam" });
        await using var browser = new RenderBrowser();
        using var engine = EngineWith(browser, RenderOptions.DEFAULT_TIMEOUT, $"http://127.0.0.1:{ClosedPort()}");

        var error = await Should.ThrowAsync<RenderingFailedException>(() => engine.RenderPdfAsync(deck, CancellationToken.None));

        error.Message.ShouldStartWith(PlaywrightRenderEngine.FAILED);
        error.InnerException.ShouldBeNull();
        var text = error.ToString();
        text.ShouldNotContain("token=");
        var tokens = AcceptanceApp.Services.GetRequiredService<IRenderTokens>();
        System.Text.RegularExpressions.Regex.Matches(text, "[A-Za-z0-9_.~=+/-]{16,}")
            .Select(match => match.Value)
            .Where(candidate => tokens.Validate(candidate, deck, RenderPurpose.Print))
            .ShouldBeEmpty("no live Print token for the deck may appear in the exception");
    }

    private static PlaywrightRenderEngine EngineWith(RenderBrowser browser, TimeSpan timeout, string renderUrl)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [RenderOptions.URL_SETTING] = renderUrl,
            [RenderOptions.TIMEOUT_SETTING] = timeout.ToString("c"),
        }).Build();
        return new PlaywrightRenderEngine(
            browser,
            AcceptanceApp.Services.GetRequiredService<IRenderTokens>(),
            configuration,
            AcceptanceApp.Services.GetRequiredService<IServer>(),
            AcceptanceApp.Services.GetRequiredService<IPublicUrls>());
    }

    // A loopback port that was free a moment ago and has nothing listening on it now.
    private static int ClosedPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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
