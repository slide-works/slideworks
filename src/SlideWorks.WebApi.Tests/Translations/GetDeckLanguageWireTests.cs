using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.ApiOnly;

namespace SlideWorks.WebApi.Tests.Translations;

// get_deck binds language on the wire: on a deck with translations off, a language other than the primary is refused, naming
// the deck's only language (SW-883 K4).
[TestFixture]
[CancelAfter(60000)]
public class GetDeckLanguageWireTests : ApiTestFixture
{
    [Test]
    public async Task GetDeck_WhenLanguageGivenOnWireAndTranslationsOff_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var seed = await McpWireContext.SeedAsync(Factory, Client, agent);

        var result = await seed.CallOnWireAsync("get_deck", $$$"""{"deck":"{{{seed.Deck}}}","language":"th"}""");

        var raw = result.GetRawText();
        result.GetProperty("isError").GetBoolean().ShouldBeTrue(raw);
        McpWire.TextOf(result).ShouldBe("th is not one of this deck's languages (en)");
    }
}
