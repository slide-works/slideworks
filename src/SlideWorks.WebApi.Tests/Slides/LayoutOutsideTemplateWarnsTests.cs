using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Slides;

// D9, L7: a slide whose layout is outside the deck's template is accepted and kept, with a warning in set_look's wording.
// A layout the template allows gives no warning.
[TestFixture]
public class LayoutOutsideTemplateWarnsTests : ApiTestFixture
{
    private const string QUOTE = """{"quote":"Ship it","attribution":"Sam Rivera"}""";

    private const string CARDS = """{"headline":"Why","cards":[{"title":"One","text":"First.","chips":[]}]}""";
    private const string STATEMENT = """{"statement":"Ship smaller"}""";
    private const string GANTT = """{"headline":"Plan","columns":["Q1","Q2"],"rows":[{"label":"Build","start":1,"end":2,"status":"done"}]}""";

    private Template _template = null!;

    [SetUp]
    public void PickATemplateWithoutQuote()
        => _template = new TemplateCatalogue().All.First(t => !t.Allows("quote"));

    [Test]
    public async Task AddSlide_WhenLayoutOutsideTheTemplate_ShouldAcceptItWithAWarning()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await CreateDeckAsync(agent, _template);

        var result = await AddAsync(agent, deck, "quote", QUOTE);

        Warnings(result).ShouldBe([$"Slide 1: layout 'quote' is not in the '{_template.Name}' template"]);
        (await SlideCalls.SlidesAsync(agent, deck)).Single().GetProperty("layout").GetString().ShouldBe("quote");
    }

    [Test]
    public async Task UpdateSlide_WhenLayoutOutsideTheTemplate_ShouldAcceptItWithAWarning()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await CreateDeckAsync(agent, _template);
        var revision = (await AddAsync(agent, deck, "quote", QUOTE)).GetProperty("revision").GetInt64();

        var result = await SlideCalls.UpdateAsync(agent, deck, 1, revision, QUOTE);

        Warnings(result).ShouldBe([$"Slide 1: layout 'quote' is not in the '{_template.Name}' template"]);
    }

    [Test]
    public async Task AddSlide_WhenLayoutInTheTemplate_ShouldGiveNoWarnings()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await CreateDeckAsync(agent, new TemplateCatalogue().All.First(t => t.Allows("quote")));

        var result = await AddAsync(agent, deck, "quote", QUOTE);

        Warnings(result).ShouldBeEmpty();
    }

    [Test]
    public async Task AddSlide_WhenCardsOnTalk_ShouldNotWarn()
    {
        var result = await AddOnTalkAsync("cards", CARDS);

        Warnings(result).ShouldBeEmpty();
    }

    [Test]
    public async Task AddSlide_WhenStatementOnTalk_ShouldNotWarn()
    {
        var result = await AddOnTalkAsync("statement", STATEMENT);

        Warnings(result).ShouldBeEmpty();
    }

    [Test]
    public async Task AddSlide_WhenGanttOnTalk_ShouldWarn()
    {
        var result = await AddOnTalkAsync("gantt", GANTT);

        Warnings(result).ShouldBe(["Slide 1: layout 'gantt' is not in the 'talk' template"]);
    }

    private async Task<JsonElement> AddOnTalkAsync(string layout, string slotsJson)
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await CreateDeckAsync(agent, new TemplateCatalogue().Find("talk")!);

        return await AddAsync(agent, deck, layout, slotsJson);
    }

    private static async Task<string> CreateDeckAsync(McpTestClient agent, Template template)
        => (await SlideCalls.OkAsync(agent, "create_deck", new { title = "Ship smaller", template = template.Name })).GetProperty("deck").GetString()!;

    private static async Task<JsonElement> AddAsync(McpTestClient agent, string deck, string layout, string slotsJson)
    {
        using var slots = JsonDocument.Parse(slotsJson);
        return await SlideCalls.OkAsync(agent, "add_slide", new { deck, layout, slots = slots.RootElement });
    }

    private static IEnumerable<string?> Warnings(JsonElement result) => result.GetProperty("warnings").EnumerateArray().Select(w => w.GetString());
}
