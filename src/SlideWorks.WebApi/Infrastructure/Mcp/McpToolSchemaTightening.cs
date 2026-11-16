using Agoda.IoC.Core;
using ModelContextProtocol.Server;

namespace SlideWorks.WebApi.Infrastructure.Mcp;

// Applies McpToolSchemas.Tighten to each tool exactly once for the app's lifetime. The tools are singletons, but in
// stateless mode the SDK builds McpServerOptions for every request, so the PostConfigure in McpSetup that calls Apply runs
// on every request too. Without this guard each request rewrote every tool's inputSchema while other requests read it.
// Every reader of a schema (tools/list, McpArgumentCheck, the transport's header check) gets its tools through those
// options, so it has passed Apply, and with it this lock, before it reads: no request sees a schema half written or not
// yet tightened.
[RegisterSingleton]
public sealed partial class McpToolSchemaTightening(ILogger<McpToolSchemaTightening> logger)
{
    private readonly Lock _gate = new();

    private readonly HashSet<McpServerTool> _tightened = new(ReferenceEqualityComparer.Instance);

    public void Apply(IEnumerable<McpServerTool> tools)
    {
        lock (_gate)
        {
            foreach (var tool in tools.Where(_tightened.Add).Select(tool => tool.ProtocolTool))
            {
                McpToolSchemas.Tighten(tool);
                LogTightened(tool.Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tightened the inputSchema of MCP tool {Tool}")]
    private partial void LogTightened(string tool);
}
