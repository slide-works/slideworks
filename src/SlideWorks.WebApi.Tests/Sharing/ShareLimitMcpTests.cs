using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// The guard counts only people newly added to a deck. With the limit at 2, changing a person's access over MCP at the limit
// is not a new recipient, so it goes through, and a third new person is still refused.
[TestFixture]
public class ShareLimitMcpTests : ApiTestFixture
{
    protected override Dictionary<string, string?> ExtraConfig => new() { [SharingLimits.NEW_PER_HOUR_KEY] = "2" };

    [Test]
    public async Task SetSharing_WhenReadToWriteAtTheLimit_ShouldSkipTheGuardButStillRefuseANewPerson()
    {
        var sharer = TestUser.NewApproved();
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, sharer);
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var two = ShareLimitApi.NewEmails(2);
        var third = ShareLimitApi.NewEmails(1)[0];
        await agent.CallToolAsync("set_sharing", new { deck, level = "named", viewers = two });

        var (upgradeError, upgrade) = await agent.CallToolAsync("set_sharing", new { deck, person = two[0], access = "write" });
        var (newError, refused) = await agent.CallToolAsync("set_sharing", new { deck, person = third, access = "read" });

        upgradeError.ShouldBeFalse(upgrade.ToString());
        newError.ShouldBeTrue();
        refused.GetString()!.ShouldStartWith("You can share with 2 new people an hour.");
    }
}
