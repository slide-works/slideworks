using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.ApiOnly;

namespace SlideWorks.WebApi.Tests.Translations;

// translate_slides arguments, with a null line, bind on the wire, and the null removes the line.
[TestFixture]
[CancelAfter(60000)]
public class TranslateSlidesWireTests : ApiTestFixture
{
    [Test]
    public async Task TranslateSlides_WhenCalledOnWireWithNullLine_ShouldBind()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var seed = await McpWireContext.SeedAsync(Factory, Client, agent);

        await new TranslationRowSeed(Factory.Services).SettingsAsync(seed.Deck, true, "en", "th");
        var revision = await seed.RevisionOfSlideAsync(1);
        var result = await seed.CallOnWireAsync("translate_slides", $$$"""{"deck":"{{{seed.Deck}}}","language":"th","slides":[{"slide":1,"revision":{{{revision}}},"lines":{"/quote":"Ship it","/attribution":null}}]}""");

        var raw = result.GetRawText();
        (result.TryGetProperty("isError", out var flag) && flag.GetBoolean()).ShouldBeFalse(raw);
        result.GetProperty("structuredContent").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Object, raw);
    }

    [Test]
    public async Task TranslateSlides_WhenNullGivenOnWire_ShouldRemoveLine()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var seed = await McpWireContext.SeedAsync(Factory, Client, agent);
        var rows = new TranslationRowSeed(Factory.Services);
        await rows.SettingsAsync(seed.Deck, true, "en", "th");
        await rows.LinesAsync(seed.Deck, 1, new Dictionary<string, IReadOnlyDictionary<string, (string Text, string From)>>
        {
            ["th"] = new Dictionary<string, (string Text, string From)> { ["/quote"] = ("ชุดเล็ก", "Small batches ship sooner"), ["/attribution"] = ("แซม", "Sam Rivera") },
        });
        var revision = await seed.RevisionOfSlideAsync(1);

        var result = await seed.CallOnWireAsync("translate_slides", $$$"""{"deck":"{{{seed.Deck}}}","language":"th","slides":[{"slide":1,"revision":{{{revision}}},"lines":{"/attribution":null}}]}""");

        var raw = result.GetRawText();
        (result.TryGetProperty("isError", out var flag) && flag.GetBoolean()).ShouldBeFalse(raw);
        var slide = result.GetProperty("structuredContent").GetProperty("slides")[0];
        slide.GetProperty("lines").TryGetProperty("/attribution", out _).ShouldBeFalse(raw);
        slide.GetProperty("lines").GetProperty("/quote").GetString().ShouldBe("ชุดเล็ก");
        slide.GetProperty("missing").GetInt32().ShouldBe(1);
        await using var scope = Factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Slides.AsNoTracking()
            .Where(s => s.DeckId == seed.Deck && s.DeletedAt == null && s.Position == 0).Select(s => s.TranslationsJson).SingleAsync();
        stored.ShouldNotBeNull();
        stored.ShouldNotContain("/attribution", Case.Sensitive);
        stored.ShouldContain("ชุดเล็ก", Case.Sensitive);
    }
}
