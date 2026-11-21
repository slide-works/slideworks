using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Mcp;

[TestFixture]
[CancelAfter(60000)]
public class McpToolListTests : ApiTestFixture
{
    [Test]
    public async Task ToolList_WhenServerLists_ShouldMatchExpectedTools()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var registered = (await agent.Client.ListToolsAsync()).Select(t => t.Name).Order(StringComparer.Ordinal).ToList();

        var message = "The tools the server registers differ from ExpectedTools.Names (src/SlideWorks.WebApi.Tests/Support/ExpectedTools.cs). "
            + "This is the one place to add or remove a tool name. "
            + $"Not in the list: [{string.Join(", ", registered.Except(ExpectedTools.Names, StringComparer.Ordinal))}]. "
            + $"Not registered: [{string.Join(", ", ExpectedTools.Names.Except(registered, StringComparer.Ordinal))}].";
        registered.ShouldBe(ExpectedTools.Names, message);
    }
}
