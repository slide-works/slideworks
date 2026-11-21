using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure.Rendering;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Checks;

// SW-3 D13: the deck page's own read carries the stored findings (DeckPageView.findings), checked against the deck as it
// is now. Nothing renders. Who may read the page is DeckPageTests' business, so only the one shared edge is kept here.
[TestFixture]
[CancelAfter(60000)]
public class DeckPageFindingsTests : ApiTestFixture
{
    private static readonly string BULLETS = JsonSerializer.Serialize(new { headline = "Why", items = Enumerable.Repeat(new string('a', 64), 6) });

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    [Test]
    public async Task Get_WhenTheOwnerAsks_ShouldMatchTheScreenShapeAndNotRender()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        var engine = Factory.Services.GetRequiredService<FakeRenderEngine>();
        engine.Reset();

        var response = await Client.GetAsync($"/api/decks/{deck}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        JsonShape.AssertMatches(document.RootElement, "deck-page.json", ScreenShapes.DeckPage);
        document.RootElement.GetProperty("findings").GetArrayLength().ShouldBe(0);
        engine.Calls.ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenTheLookChanged_ShouldReturnTheFitFinding()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = (await SlideCalls.OkAsync(agent, "create_deck", new { title = "Ship smaller", template = "workshop" })).GetProperty("deck").GetString()!;
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        // The fake is shared by the fixture and answers by ref, so only this test's deck is on the workshop template.
        Factory.Services.GetRequiredService<FakeTemplates>()
            .Add(BuiltInTemplates.Resolve(new TemplateCatalogue().Find("workshop")!) with { FitWidths = new FitWidths(0.7, 0.7, 0.7) });

        var response = await Client.GetAsync($"/api/decks/{deck}");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        JsonShape.AssertMatches(document.RootElement, "deck-page.json", ScreenShapes.DeckPage);
        var finding = document.RootElement.GetProperty("findings").EnumerateArray().ShouldHaveSingleItem();
        finding.GetProperty("slide").GetInt32().ShouldBe(1);
        finding.GetProperty("rule").GetString().ShouldBe("fit");
        finding.GetProperty("source").GetString().ShouldBe("stored");
    }
}
