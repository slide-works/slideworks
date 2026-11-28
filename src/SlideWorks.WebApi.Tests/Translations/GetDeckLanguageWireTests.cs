using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.ApiOnly;

namespace SlideWorks.WebApi.Tests.Translations;

// get_deck accepts language on the wire.
[TestFixture]
[CancelAfter(60000)]
public class GetDeckLanguageWireTests : ApiTestFixture
{
    [Test]
    public async Task GetDeck_WhenLanguageGivenOnWire_ShouldBind()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var seed = await McpWireContext.SeedAsync(Factory, Client, agent);

        var result = await seed.CallOnWireAsync("get_deck", $$$"""{"deck":"{{{seed.Deck}}}","language":"th"}""");

        var raw = result.GetRawText();
        (result.TryGetProperty("isError", out var flag) && flag.GetBoolean()).ShouldBeFalse(raw);
        result.GetProperty("structuredContent").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Object, raw);
    }
}
