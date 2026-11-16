namespace SlideWorks.WebApi.Features.Decks.Views;

// The library's "connect an agent" card: whether to show it, and the absolute MCP URL to paste into an agent.
public sealed record ConnectAgent(bool Show, string McpUrl);
