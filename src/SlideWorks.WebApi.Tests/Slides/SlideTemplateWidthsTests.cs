using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using static SlideWorks.WebApi.Tests.Slides.SlideCalls;

namespace SlideWorks.WebApi.Tests.Slides;

// K2 through the seam (T4): a slide is wrapped with the glyph widths of its deck's template, and its layout is the one
// the template finds. The fixture's ITemplates is the fake; a deck made on the built-in "talk" stores that ref, and the
// fake answers a template added under it before the built-in.
[TestFixture]
[CancelAfter(30000)]
public class SlideTemplateWidthsTests : ApiTestFixture
{
    // Six points of 70 letters: one line each at 0.56 em a letter (71 fit), two at 0.62 (64 fit), which is too tall.
    private static readonly string BULLETS = JsonSerializer.Serialize(new { headline = "Why", items = Enumerable.Repeat(new string('a', 70), 6) });

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    // One test holds every step: the fake is shared by the fixture, and "talk" is wide from the Add on.
    [Test]
    public async Task AddSlide_WhenTheTemplateIsWider_ShouldRefuseTextTheBuiltInAccepts()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var accepted = await DeckSeed.AddSlideAsync(agent, deck, "bullets", BULLETS);
        var before = (await OkAsync(agent, "get_deck", new { deck })).GetRawText();
        var talk = new TemplateCatalogue().Default;
        Factory.Services.GetRequiredService<FakeTemplates>().Add(BuiltInTemplates.Resolve(talk) with { FitWidths = new FitWidths(0.62, 0.62, 0.62) });

        var added = await ErrorAsync(agent, "add_slide", new { deck, layout = "bullets", slots = Slots(BULLETS) });
        var updated = await ErrorAsync(agent, "update_slide", new { deck, slide = 1, revision = 1, slots = Slots(BULLETS) });

        accepted.GetProperty("slide").GetInt32().ShouldBe(1);
        talk.Name.ShouldBe("talk");
        added.ShouldStartWith("bullets.items: ");
        updated.ShouldBe(added);
        (await OkAsync(agent, "get_deck", new { deck })).GetRawText().ShouldBe(before);
    }

    // The layout is the one the template finds: a name the built-in catalogue does not hold is validated and its steps
    // counted from the template's definition.
    [Test]
    public async Task AddSlide_WhenOnlyTheTemplateFindsTheLayout_ShouldValidateWithItsDefinition()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var workshop = new TemplateCatalogue().All.First(t => !t.IsDefault);
        var (isError, created) = await agent.CallToolAsync("create_deck", new { title = "On another template", template = workshop.Name });
        isError.ShouldBeFalse(created.ToString());
        var deck = created.GetProperty("deck").GetString()!;
        var own = new LayoutDefinition("only-here", "Found by this template only.", LayoutSurface.Light, [SlotKit.Req("headline", new TextSlot(40))]);
        var unknown = await ErrorAsync(agent, "add_slide", new { deck, layout = own.Name, slots = Slots("""{ "headline": "Only here" }""") });
        Factory.Services.GetRequiredService<FakeTemplates>()
            .Add(BuiltInTemplates.Resolve(workshop) with { Layouts = name => name == own.Name ? own : LayoutCatalogue.Find(name) });

        var added = await OkAsync(agent, "add_slide", new { deck, layout = own.Name, slots = Slots("""{ "headline": "Only here" }""") });
        var refused = await ErrorAsync(agent, "add_slide", new { deck, layout = own.Name, slots = Slots("""{ "headline": null }""") });

        unknown.ShouldStartWith("layout: unknown layout 'only-here'");
        added.GetProperty("slide").GetInt32().ShouldBe(1);
        added.GetProperty("steps").GetInt32().ShouldBe(0);
        added.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldBe([$"Slide 1: layout 'only-here' is not in the '{workshop.Name}' template"]);
        refused.ShouldBe("only-here.headline: required");
    }

    private static JsonElement Slots(string json)
    {
        using var slots = JsonDocument.Parse(json);
        return slots.RootElement.Clone();
    }
}
