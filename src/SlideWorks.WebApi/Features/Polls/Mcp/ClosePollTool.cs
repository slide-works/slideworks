using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Polls.Mcp;

[McpServerToolType]
public sealed class ClosePollTool
{
    [McpServerTool(Name = "close_poll")]
    [Description("Close a poll slide's open round in a deck you can edit that a person has taken live. The round's result is kept; closing doesn't start another. Returns the presenter's view of the poll: phase, round number, answered, joined, counts and settings.")]
    public Task<CallToolResult> ClosePoll(
        PollAgentAccess access,
        [Description("The deck's id.")] string deck,
        [Description("The poll slide's 1-based number, as get_deck gave it.")] int slide)
        => McpToolErrors.Run(() => access.CloseAsync(deck, slide));
}
