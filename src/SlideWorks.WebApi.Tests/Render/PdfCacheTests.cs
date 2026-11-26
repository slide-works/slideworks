using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Render;

// The PDF cache (P5, D7, D13), through GET /api/decks/{id}/pdf as a presenter: an unchanged deck and talk reuse the PDF
// on the data volume; a new revision or session renders again; overlapping downloads share one render; a failed render
// is never kept; and a new PDF replaces the deck's older PDFs but not its cards. FakeRenderEngine counts the PDF renders.
[TestFixture]
[CancelAfter(60000)]
public class PdfCacheTests : ApiTestFixture
{
    private FakeRenderEngine Engine => Factory.Services.GetRequiredService<FakeRenderEngine>();

    [TearDown]
    public void ResetEngine() => Engine.Reset();

    [Test]
    public async Task GetPdf_WhenAskedTwiceForAnUnchangedDeck_ShouldRenderOnceAndReturnTheSameBytes()
    {
        var deck = await CreateDeckWithSlideAsync("Unchanged");

        var first = await Client.GetAsync($"/api/decks/{deck}/pdf");
        var second = await Client.GetAsync($"/api/decks/{deck}/pdf");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.Content.ReadAsByteArrayAsync()).ShouldBe(await first.Content.ReadAsByteArrayAsync());
        PdfRendersOf(deck).ShouldBe(1);
        PdfFiles(deck).Length.ShouldBe(1);
    }

    [Test]
    public async Task GetPdf_WhenTheDeckChangesBetweenDownloads_ShouldRenderAgain()
    {
        var deck = await CreateDeckWithSlideAsync("Edited");
        (await Client.GetAsync($"/api/decks/{deck}/pdf")).EnsureSuccessStatusCode();

        await AddSlideAsync(deck, "A second slide");
        (await Client.GetAsync($"/api/decks/{deck}/pdf")).EnsureSuccessStatusCode();

        PdfRendersOf(deck).ShouldBe(2);
    }

    [Test]
    public async Task GetPdf_WhenANewSessionStartsBetweenDownloads_ShouldRenderAgain()
    {
        var deck = await CreateDeckWithSlideAsync("Goes live");
        (await Client.GetAsync($"/api/decks/{deck}/pdf")).EnsureSuccessStatusCode();

        (await Client.PostAsync($"/api/decks/{deck}/live", null)).EnsureSuccessStatusCode();
        (await Client.GetAsync($"/api/decks/{deck}/pdf")).EnsureSuccessStatusCode();

        PdfRendersOf(deck).ShouldBe(2);
    }

    [Test]
    public async Task GetPdf_WhenARenderFails_ShouldNotKeepItAndTheNextDownloadRendersAgain()
    {
        var deck = await CreateDeckWithSlideAsync("Fails once");
        Engine.ThrowOnRender = new RenderingFailedException("an image failed to load");

        var failed = await Client.GetAsync($"/api/decks/{deck}/pdf");
        Engine.ThrowOnRender = null;
        var retried = await Client.GetAsync($"/api/decks/{deck}/pdf");

        failed.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        retried.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await retried.Content.ReadAsByteArrayAsync()).ShouldBe(FakeRenderEngine.PDF);
        PdfRendersOf(deck).ShouldBe(2);
    }

    [Test]
    public async Task GetPdf_WhenTwoDownloadsOverlap_ShouldShareOneRender()
    {
        var deck = await CreateDeckWithSlideAsync("Overlap");
        var hold = Engine.HoldPdfRenders();

        var first = Client.GetAsync($"/api/decks/{deck}/pdf");
        var second = Client.GetAsync($"/api/decks/{deck}/pdf");
        await WaitForAsync(() => PdfRendersOf(deck) >= 1);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        hold.Dispose();
        var responses = await Task.WhenAll(first, second);

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.OK);
        PdfRendersOf(deck).ShouldBe(1);
    }

    [Test]
    public async Task GetPdf_WhenANewPdfIsStored_ShouldDeleteTheDecksOlderPdfsButKeepItsCards()
    {
        var deck = await CreateDeckWithSlideAsync("Tidy");
        var folder = RendersFolder(deck);
        (await Client.GetAsync($"/api/decks/{deck}/pdf")).EnsureSuccessStatusCode();
        var card = Path.Combine(folder, "card-1-abc-link.png");
        await File.WriteAllBytesAsync(card, FakeRenderEngine.PNG);
        var older = PdfFiles(deck).ShouldHaveSingleItem();

        await AddSlideAsync(deck, "Another slide");
        (await Client.GetAsync($"/api/decks/{deck}/pdf")).EnsureSuccessStatusCode();

        var current = PdfFiles(deck).ShouldHaveSingleItem();
        current.ShouldNotBe(older);
        File.Exists(card).ShouldBeTrue();
    }

    private int PdfRendersOf(string deck) => Engine.Calls.Count(call => call == $"pdf:{deck}");

    private string RendersFolder(string deck) =>
        Path.Combine(Factory.Services.GetRequiredService<IConfiguration>()[ImageStore.DATA_PATH_SETTING]!, "renders", deck);

    private string[] PdfFiles(string deck) =>
        Directory.Exists(RendersFolder(deck)) ? Directory.GetFiles(RendersFolder(deck), "pdf-*.pdf") : [];

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var attempt = 0; !condition() && attempt < 1000; attempt++)
        {
            await Task.Delay(20);
        }
    }

    private async Task AddSlideAsync(string deck, string quote)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        await DeckSeed.AddQuoteAsync(agent, deck, quote);
    }

    private async Task<string> CreateDeckWithSlideAsync(string title)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent, title, author: "Sam Rivera");
        await DeckSeed.AddQuoteAsync(agent, deck, "One slide is enough");
        return deck;
    }
}
