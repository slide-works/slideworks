namespace SlideWorks.WebApi.Infrastructure.Mcp;

// The authorization policy on /mcp and the one authentication scheme it accepts. The browser's cookie is never consulted.
public static class AgentPolicy
{
    public const string NAME = "Agent";

    public const string SCHEME = "AgentBearer";
}
