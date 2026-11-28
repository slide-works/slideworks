using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.ApiOnly;

namespace SlideWorks.WebApi.Tests.Translations;

// set_translations arguments bind on the wire and the result is an object.
[TestFixture]
[CancelAfter(60000)]
public class SetTranslationsWireTests : ApiTestFixture
{
    [Test]
    public async Task SetTranslations_WhenCalledOnWire_ShouldReturnObject()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var seed = await McpWireContext.SeedAsync(Factory, Client, agent);

        var result = await seed.CallOnWireAsync("set_translations", $$$"""{"deck":"{{{seed.Deck}}}","enabled":true,"languages":["es"]}""");

        var raw = result.GetRawText();
        (result.TryGetProperty("isError", out var flag) && flag.GetBoolean()).ShouldBeFalse(raw);
        var content = result.GetProperty("structuredContent");
        content.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Object, raw);
        var translations = content.GetProperty("translations");
        translations.GetProperty("enabled").GetBoolean().ShouldBeTrue(raw);
        translations.GetProperty("languages")[0].GetProperty("code").GetString().ShouldBe("es", raw);
    }
}
