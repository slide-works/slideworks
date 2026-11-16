using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SlideWorks.WebApi.Infrastructure.Mcp;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Mcp;

// The tools are singletons, but in stateless mode the SDK builds its options, and so runs McpSetup's PostConfigure, for
// every MCP request. Each tool's inputSchema must still be tightened exactly once for the app's lifetime, never rewritten
// while another request reads it. Serilog owns the logger factory, so McpToolSchemaTightening's logger is replaced for
// its own category to count what it tightened.
[TestFixture]
[CancelAfter(60000)]
public class McpToolSchemaTighteningTests : ApiTestFixture
{
    private const int TOOLS = 13;

    private readonly TighteningLog _log = new();

    protected override Action<IServiceCollection>? ConfigureTestServices
        => services => services.AddSingleton<ILogger<McpToolSchemaTightening>>(_log);

    [Test]
    public async Task ManyRequests_ShouldTightenEachToolOnceForTheAppsLifetime()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        for (var i = 0; i < 20; i++)
        {
            await McpWire.InputSchemasAsync(Factory, agent.Tokens.AccessToken);
            await McpWire.CallToolAsync(Factory, agent.Tokens.AccessToken, "list_layouts", "{}");
        }
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => McpWire.InputSchemasAsync(Factory, agent.Tokens.AccessToken)));

        _log.Tools.Count.ShouldBe(TOOLS, string.Join(", ", _log.Tools));
        _log.Tools.Distinct(StringComparer.Ordinal).Count().ShouldBe(TOOLS);
    }

    [Test]
    public async Task ToolsList_When20AtOnce_ShouldAnswerEveryOneIdentically()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var answers = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => McpWire.SendAsync(
            Factory,
            agent.Tokens.AccessToken,
            """{"jsonrpc":"2.0","id":7,"method":"tools/list","params":{}}""")));

        var first = answers[0].GetProperty("result").GetRawText();
        answers[0].GetProperty("result").GetProperty("tools").GetArrayLength().ShouldBe(TOOLS);
        foreach (var answer in answers)
        {
            answer.GetProperty("result").GetRawText().ShouldBe(first);
        }
    }

    private sealed class TighteningLog : ILogger<McpToolSchemaTightening>
    {
        private readonly Lock _gate = new();

        private readonly List<string> _tools = [];

        public IReadOnlyList<string> Tools
        {
            get
            {
                lock (_gate)
                {
                    return [.. _tools];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object?>> values && values.FirstOrDefault(value => value.Key == "Tool").Value is string tool)
            {
                lock (_gate)
                {
                    _tools.Add(tool);
                }
            }
        }
    }
}
