namespace SlideWorks.WebApi.Infrastructure.Setup;

// The MCP server agents connect to: Program.cs calls these two hooks, and everything about the transport lives here.
public static class McpSetup
{
    // TODO: register the MCP server, its tools and the agent bearer scheme here.
    public static IServiceCollection AddSlideWorksMcp(this IServiceCollection services) => services;

    // TODO: map the MCP endpoint at /mcp behind the agent policy here.
    public static WebApplication MapSlideWorksMcp(this WebApplication app) => app;
}
