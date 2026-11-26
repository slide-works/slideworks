using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Sharing;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// GET /api/decks/{id}/pdf: a presenter gets the deck as a PDF attachment, through the real IPdfRenderer to the fake engine.
// A viewer, an unknown deck and a deck with no slides are refused before anything renders. A renderer that can't run,
// times out or fails is 503 with the worded error, and a failure's text never holds the render token.
[TestFixture]
[CancelAfter(60000)]
public class DeckPdfTests : ApiTestFixture
{
    private FakeRenderEngine Engine => Factory.Services.GetRequiredService<FakeRenderEngine>();

    [TearDown]
    public void ResetEngine() => Engine.Reset();

    [Test]
    public async Task GetPdf_WhenPresenter_ShouldReturnThePdfAsAnAttachment()
    {
        var deck = await CreateDeckWithSlideAsync("Ship smaller");

        var response = await Client.GetAsync($"/api/decks/{deck}/pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition.FileName.ShouldBe("ship-smaller.pdf");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(FakeRenderEngine.PDF);
    }

    [Test]
    public async Task GetPdf_WhenViewerOrUnknownDeck_ShouldBe404AndNotRender()
    {
        var deck = await CreateDeckWithSlideAsync("Viewer deck");
        await SharingApi.PutOkAsync(Client, deck, "named", TestUser.Alex.Email);
        using var alex = ClientFor(TestUser.Alex);

        var viewer = await alex.GetAsync($"/api/decks/{deck}/pdf");
        var unknown = await Client.GetAsync("/api/decks/no-such-deck/pdf");

        viewer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await viewer.Content.ReadFromJsonAsync<ErrorBody>())!.Error.ShouldBe("Deck not found");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unknown.Content.ReadFromJsonAsync<ErrorBody>())!.Error.ShouldBe("Deck not found");
        Engine.Calls.ShouldNotContain($"pdf:{deck}");
        Engine.Calls.ShouldNotContain("pdf:no-such-deck");
    }

    [Test]
    public async Task GetPdf_WhenTheDeckHasNoSlides_ShouldBe400AndNotRender()
    {
        var deck = await SharingApi.CreateDeckAsync(Client, "Empty deck");

        var response = await Client.GetAsync($"/api/decks/{deck}/pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error.ShouldBe("The deck has no slides to print.");
        Engine.Calls.ShouldNotContain($"pdf:{deck}");
    }

    [Test]
    public async Task GetPdf_WhenRendererUnavailable_ShouldBe503WithTheWordedError()
    {
        var deck = await CreateDeckWithSlideAsync("Unavailable");
        Engine.ThrowUnavailable();

        var response = await Client.GetAsync($"/api/decks/{deck}/pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error.ShouldBe("PDF export isn't available right now. Try again in a minute.");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Test]
    public async Task GetPdf_WhenTheRenderTimesOut_ShouldBe503WithTheWordedError()
    {
        var deck = await CreateDeckWithSlideAsync("Slow");
        Engine.ThrowTimeout();

        var response = await Client.GetAsync($"/api/decks/{deck}/pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error.ShouldBe("Making the PDF took too long. Try again.");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Test]
    public async Task GetPdf_WhenTheRenderFails_ShouldBe503WithTheReasonAndNoToken()
    {
        var deck = await CreateDeckWithSlideAsync("Broken logo");
        Engine.ThrowOnRender = new RenderingFailedException("an image failed to load");

        var response = await Client.GetAsync($"/api/decks/{deck}/pdf");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Error.ShouldBe("Couldn't make the PDF: an image failed to load");
        body.ShouldNotContain("token");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Test]
    public async Task GetPdf_WhenAnonymous_ShouldBeRefused()
    {
        var deck = await CreateDeckWithSlideAsync("Anonymous");
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync($"/api/decks/{deck}/pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Engine.Calls.ShouldNotContain($"pdf:{deck}");
    }

    private async Task<string> CreateDeckWithSlideAsync(string title)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, title, author: "Sam Rivera");
        await DeckSeed.AddQuoteAsync(agent, deck, "One slide is enough");
        return deck;
    }

    private sealed record ErrorBody(string Error);
}
