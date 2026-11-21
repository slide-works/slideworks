using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Images;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Checks;

// SW-3 K6: the stored checks run again on every slide against the deck as it is now, through check_deck on the wire
// (FakeRenderEngine measures nothing, so every finding here is a stored one).
[TestFixture]
[CancelAfter(60000)]
public class StoredChecksTests : ApiTestFixture
{
    // Six points of 64 letters: one line each at the default 0.56 em a letter, two at 0.7, which is too tall.
    private static readonly string BULLETS = JsonSerializer.Serialize(new { headline = "Why", items = Enumerable.Repeat(new string('a', 64), 6) });

    private static readonly string LONG_STATEMENT = string.Join(" ", Enumerable.Repeat("Ship small.", 5));

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    [TearDown]
    public void ForgetWidenedTemplates() => Factory.Services.GetRequiredService<FakeTemplates>().Reset();

    [Test]
    public async Task CheckDeck_WhenTheFontsGetWiderAfterASlideWasAdded_ShouldReportFit()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await CreateDeckOnAsync(agent, "workshop");
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        var before = await FindingsAsync(agent, deck);
        Widen("workshop");

        var after = await FindingsAsync(agent, deck);

        before.ShouldBeEmpty();
        var fit = after.ShouldHaveSingleItem();
        fit.GetProperty("slide").GetInt32().ShouldBe(1);
        fit.GetProperty("rule").GetString().ShouldBe("fit");
        fit.GetProperty("severity").GetString().ShouldBe("error");
        fit.GetProperty("source").GetString().ShouldBe("stored");
        fit.GetProperty("where").GetString().ShouldBe("bullets.items");
        fit.GetProperty("message").GetString()!.ShouldStartWith("bullets.items: ");
        fit.GetProperty("message").GetString()!.ShouldContain("It fitted when it was added; the deck's look has changed since.");
    }

    [Test]
    public async Task CheckDeck_WhenNothingChanged_ShouldReportNoStoredFindings()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        await DeckSeed.AddQuoteAsync(agent, deck, "Ship small.");

        var findings = await FindingsAsync(agent, deck);

        findings.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckDeck_WhenALayoutIsOutsideTheTemplate_ShouldWarnAndStillCheckFit()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var template = new TemplateCatalogue().Find("update")!;
        template.Allows("statement").ShouldBeFalse();
        var deck = await CreateDeckOnAsync(agent, template.Name);
        await DeckSeed.AddSlideAsync(agent, deck, "statement", JsonSerializer.Serialize(new { statement = LONG_STATEMENT }));
        var plain = await FindingsAsync(agent, deck);
        Factory.Services.GetRequiredService<FakeTemplates>().Add(BuiltInTemplates.Resolve(template) with
        {
            Layouts = name => name == "statement" ? null : LayoutCatalogue.Find(name),
            FitWidths = new FitWidths(1.5, 1.5, 1.5),
        });

        var wide = await FindingsAsync(agent, deck);

        plain.Select(f => f.GetProperty("rule").GetString()).ShouldBe(["layoutOutsideTemplate"]);
        wide.Select(f => f.GetProperty("rule").GetString()).ShouldBe(["fit", "layoutOutsideTemplate"]);
        wide[1].GetProperty("severity").GetString().ShouldBe("warning");
        wide[1].GetProperty("where").GetString().ShouldBe("statement");
        wide[1].GetProperty("message").GetString().ShouldBe($"Layout 'statement' is not in the '{template.Name}' template. Pick a layout from list_layouts, or change the look.");
        wide[0].GetProperty("where").GetString().ShouldStartWith("statement");
    }

    [TestCase("Image")]
    [TestCase("photo.png")]
    [TestCase("IMAGE_ID")]
    public async Task CheckDeck_WhenAltTextIsGeneric_ShouldWarn(string alt)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));
        await AddImageSlideAsync(agent, deck, image, alt == "IMAGE_ID" ? image : alt);

        var findings = await FindingsAsync(agent, deck);

        var warning = findings.ShouldHaveSingleItem();
        warning.GetProperty("rule").GetString().ShouldBe("genericAlt");
        warning.GetProperty("severity").GetString().ShouldBe("warning");
        warning.GetProperty("source").GetString().ShouldBe("stored");
        warning.GetProperty("where").GetString().ShouldBe("image.image");
        warning.GetProperty("slide").GetInt32().ShouldBe(1);
        warning.GetProperty("message").GetString().ShouldBe($"Alt text \"{(alt == "IMAGE_ID" ? image : alt)}\" doesn't say what the picture shows; describe it in a few words.");
    }

    [Test]
    public async Task CheckDeck_WhenAltTextDescribesThePicture_ShouldNotWarn()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));
        await AddImageSlideAsync(agent, deck, image, "A story map with three rows of cards");

        var findings = await FindingsAsync(agent, deck);

        findings.ShouldBeEmpty();
    }

    [Test]
    public async Task CheckDeck_WhenSlidesAreListed_ShouldCheckOnlyThose()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var image = await ImageSeed.UploadAsync(agent, Factory, deck, ImageSeed.Bytes("image/png"));
        await AddImageSlideAsync(agent, deck, image, "Image");
        await AddImageSlideAsync(agent, deck, image, "Image");
        await DeckSeed.AddQuoteAsync(agent, deck, "Ship small.");

        var second = await FindingsAsync(agent, deck, [2]);
        var third = await FindingsAsync(agent, deck, [3]);
        var all = await FindingsAsync(agent, deck);

        second.Select(f => f.GetProperty("slide").GetInt32()).ShouldBe([2]);
        third.ShouldBeEmpty();
        all.Select(f => f.GetProperty("slide").GetInt32()).ShouldBe([1, 2]);
    }

    // The fake is shared by the fixture and answers by ref, so each test that widens a template has a deck on a template of its own.
    private void Widen(string template)
        => Factory.Services.GetRequiredService<FakeTemplates>()
            .Add(BuiltInTemplates.Resolve(new TemplateCatalogue().Find(template)!) with { FitWidths = new FitWidths(0.7, 0.7, 0.7) });

    private static async Task<string> CreateDeckOnAsync(McpTestClient agent, string template)
        => (await OkAsync(agent, "create_deck", new { title = "Ship smaller", template })).GetProperty("deck").GetString()!;

    private static Task<JsonElement> AddImageSlideAsync(McpTestClient agent, string deck, string image, string alt)
        => DeckSeed.AddSlideAsync(agent, deck, "image", JsonSerializer.Serialize(new { headline = "The board", image = new { image, alt } }));

    private static async Task<IReadOnlyList<JsonElement>> FindingsAsync(McpTestClient agent, string deck, int[]? slides = null)
    {
        var result = await OkAsync(agent, "check_deck", slides is null ? new { deck } : new { deck, slides });
        return [.. result.GetProperty("findings").EnumerateArray()];
    }
}
