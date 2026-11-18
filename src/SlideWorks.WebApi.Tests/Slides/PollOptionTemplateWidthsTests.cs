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

// add_poll_option checks the slide as update_slide does (T4, K2): with the poll layout the deck's template finds and
// the glyph widths of its fonts. The built-in poll layout has no rule that wraps text, so the template here brings a
// poll layout of its own that allows an option one line; only the widths differ between the accepted call and the
// refused one. The fixture's ITemplates is the fake, and "talk" is this template from the first Add on.
[TestFixture]
[CancelAfter(30000)]
public class PollOptionTemplateWidthsTests : ApiTestFixture
{
    // 56 letters at 30 px in 1000 px: one line at 0.56 em a letter (941 px), too wide at 0.62 (1042 px).
    private static readonly LayoutDefinition ONE_LINE_POLL = new(
        "poll",
        "A poll whose options each fit one line.",
        LayoutSurface.Light,
        [SlotKit.Req("question", new TextSlot(120)), SlotKit.Req("options", new ListSlot(new TextSlot(60), 2, 8))],
        rules: OneLine);

    protected override Action<IServiceCollection>? ConfigureTestServices => FakeTemplates.Register;

    [Test]
    public async Task AddPollOption_WhenTheTemplateIsWider_ShouldRefuseTextTheNarrowerOneAccepts()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddSlideAsync(agent, deck, "poll", """{ "question": "Which part should we cut?", "options": ["The intro", "The demo"] }""");
        var talk = BuiltInTemplates.Resolve(new TemplateCatalogue().Default) with { Layouts = name => name == "poll" ? ONE_LINE_POLL : LayoutCatalogue.Find(name) };
        var templates = Factory.Services.GetRequiredService<FakeTemplates>();
        templates.Add(talk);
        var accepted = await OkAsync(agent, "add_poll_option", new { deck, slide = 1, text = new string('a', 56) });
        var before = (await OkAsync(agent, "get_deck", new { deck })).GetRawText();
        templates.Add(talk with { FitWidths = new FitWidths(0.62, 0.62, 0.62) });

        var refused = await ErrorAsync(agent, "add_poll_option", new { deck, slide = 1, text = new string('b', 56) });

        accepted.GetProperty("options").GetArrayLength().ShouldBe(3);
        // The option the narrower widths took no longer fits either; neither is stored again.
        refused.ShouldBe("poll.options[2]: more than one line; poll.options[3]: more than one line");
        (await OkAsync(agent, "get_deck", new { deck })).GetRawText().ShouldBe(before);
    }

    private static void OneLine(JsonElement slots, string path, SlotValidation validation)
    {
        var tooLong = slots.GetProperty("options").EnumerateArray()
            .Select((option, index) => (Width: option.GetString()!.Length * 30 * validation.Widths.Body, Index: index))
            .Where(option => option.Width > 1000);
        foreach (var option in tooLong)
        {
            validation.Fail($"{path}.options[{option.Index}]", "more than one line");
        }
    }
}
