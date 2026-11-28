using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.ApiOnly;

namespace SlideWorks.WebApi.Tests.Translations;

// translate_slides arguments, with a null line, bind on the wire.
[TestFixture]
[CancelAfter(60000)]
public class TranslateSlidesWireTests : ApiTestFixture
{
    [Test]
    public async Task TranslateSlides_WhenCalledOnWireWithNullLine_ShouldBind()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var seed = await McpWireContext.SeedAsync(Factory, Client, agent);

        var result = await seed.CallOnWireAsync("translate_slides", $$$"""{"deck":"{{{seed.Deck}}}","language":"th","slides":[{"slide":1,"revision":1,"lines":{"/quote":"Ship it","/attribution":null}}]}""");

        var raw = result.GetRawText();
        (result.TryGetProperty("isError", out var flag) && flag.GetBoolean()).ShouldBeFalse(raw);
        result.GetProperty("structuredContent").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Object, raw);
    }
}
