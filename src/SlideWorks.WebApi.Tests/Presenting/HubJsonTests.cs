using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Presenting;

// The hub's JSON protocol (P1): enums cross /hubs/deck as camelCase strings both ways, as they do over MVC, and an integer
// enum is refused. A real HubConnection sends the arguments the way the browser does.
[TestFixture]
[CancelAfter(30000)]
public class HubJsonTests : ApiTestFixture
{
    private readonly List<PresentationStates> _connections = [];

    [TearDown]
    public async Task DisconnectAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
        _connections.Clear();
    }

    [Test]
    public async Task JoinPresentation_WhenRoleSentAsString_ShouldReturnAndPushStateWithStringEnums()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        var joined = await owner.JoinAsync(deck, "k-1", "presenter");
        var pushed = await owner.NextAsync();

        foreach (var state in new[] { joined.GetProperty("state"), pushed })
        {
            JsonShape.AssertMatches(state, ScreenShapes.PresentationStateFile, ScreenShapes.PresentationState);
            state.GetProperty("deckId").GetString().ShouldBe(deck);
            state.GetProperty("timer").GetProperty("state").GetString().ShouldBe("notStarted");
            state.GetProperty("connected")[0].GetProperty("role").GetString().ShouldBe("presenter");
        }
        joined.GetProperty("clockMinutes").GetInt32().ShouldBeInRange(0, 1439);
    }

    [TestCase("pause")]
    [TestCase("resume")]
    [TestCase("reset")]
    public async Task SetTimer_WhenCommandSentAsString_ShouldPushStateWithStringEnums(string command)
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1", "phone");
        await owner.NextAsync();

        var state = await owner.SetTimerAsync(deck, command);
        var pushed = await owner.NextAsync();

        state.GetProperty("deckId").GetString().ShouldBe(deck);
        pushed.GetProperty("timer").GetProperty("state").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.String);
    }

    [Test]
    public async Task JoinPresentation_WhenRoleSentAsInteger_ShouldBeRefused()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);

        await Should.ThrowAsync<HubException>(() => owner.Connection.InvokeAsync<object>(
            "JoinPresentation", deck, "k-1", 0, PresentationStates.LABEL, PresentationStates.TIME_ZONE, null));
    }

    [Test]
    public async Task SetTimer_WhenCommandSentAsInteger_ShouldBeRefused()
    {
        var deck = await OwnDeckAsync();
        var owner = await ConnectAsync(TestUser.Sam);
        await owner.JoinAsync(deck, "k-1");

        await Should.ThrowAsync<HubException>(() => owner.Connection.InvokeAsync<object>("SetTimer", deck, 0));
    }

    private async Task<string> OwnDeckAsync()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        return await PresentingSeed.CreateDeckAsync(agent);
    }

    private async Task<PresentationStates> ConnectAsync(TestUser user)
    {
        var connection = await PresentationStates.ConnectAsync(Factory, user);
        _connections.Add(connection);
        return connection;
    }
}
