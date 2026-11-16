using Microsoft.AspNetCore.Authentication;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Infrastructure.Setup;

// The MCP server agents connect to: Program.cs calls these two hooks, and everything about the transport lives here.
public static class McpSetup
{
    // Streamable HTTP, stateless (each request carries its own bearer and is checked on its own), with every tool in this
    // assembly discovered from its [McpServerToolType] class. The Agent policy accepts only the AgentBearer scheme.
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
            .WithToolsFromAssembly(ApiMarker.Assembly);
        return services;
    }

    public static WebApplication MapSlideWorksMcp(this WebApplication app)
    {
        app.MapMcp("/mcp").RequireAuthorization(AgentPolicy.NAME);
        return app;
    }
}
