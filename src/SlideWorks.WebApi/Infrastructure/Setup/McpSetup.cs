using Microsoft.AspNetCore.Authentication;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Infrastructure.Setup;

// The MCP server agents connect to: Program.cs calls these two hooks, and everything about the transport lives here.
public static class McpSetup
{
    // Streamable HTTP, stateless (each request carries its own bearer and is checked on its own), with every tool in this
    // assembly discovered from its [McpServerToolType] class. The Agent policy accepts only the AgentBearer scheme.
    // McpArgumentCheck turns a call whose arguments don't fit the tool's schema into a tool error that names the argument.
    public static IServiceCollection AddSlideWorksMcp(this IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AgentBearerHandler>(AgentPolicy.SCHEME, _ => { });
        services.AddAuthorizationBuilder()
            .AddPolicy(AgentPolicy.NAME, policy => policy.AddAuthenticationSchemes(AgentPolicy.SCHEME).RequireAuthenticatedUser());
        // An empty tool collection still answers tools/list (with []), so the server serves tools before the first
        // [McpServerToolType] class exists. ??= keeps the collection the discovered tools were put in.
        services.AddMcpServer(options => options.ToolCollection ??= [])
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly(ApiMarker.Assembly)
            .WithMessageFilters(filters => filters.AddIncomingFilter(McpArgumentCheck.Unreadable))
            .WithRequestFilters(filters => filters.AddCallToolFilter(McpArgumentCheck.Filter));
        // After the SDK has put every tool in the collection: close and tighten each inputSchema, which both the agent's
        // model and McpArgumentCheck read. Stateless mode builds these options on every request, so this runs on every
        // request, but McpToolSchemaTightening tightens each tool only the first time, under a lock.
        services.AddOptions<McpServerOptions>()
            .PostConfigure<McpToolSchemaTightening>((options, tightening) => tightening.Apply(options.ToolCollection ?? []));
        return services;
    }

    public static WebApplication MapSlideWorksMcp(this WebApplication app)
    {
        app.MapMcp("/mcp").RequireAuthorization(AgentPolicy.NAME);
        return app;
    }
}
