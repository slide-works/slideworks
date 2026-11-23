using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Decks;

// list_layouts serves a schema that allows null for an optional slot, so add_slide must accept it, as the validator does.
[TestFixture]
[CancelAfter(30000)]
public class AddSlideNullOptionalSlotTests : ApiTestFixture
{
    [Test]
    public async Task AddSlide_WhenOptionalSlotsAreNullOnTheWire_ShouldAcceptTheSlide()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var result = await McpWire.CallToolAsync(
            Factory,
            agent.Tokens.AccessToken,
            "add_slide",
            $$"""{"deck":"{{deck}}","layout":"quote","slots":{"quote":"Ship smaller","attribution":"Sam Rivera","kicker":null}}""");

        (result.TryGetProperty("isError", out var isError) && isError.GetBoolean()).ShouldBeFalse(result.GetRawText());
    }
}
