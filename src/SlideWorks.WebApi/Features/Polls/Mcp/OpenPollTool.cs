using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Polls.Mcp;

[McpServerToolType]
public sealed class OpenPollTool
{
    [McpServerTool(Name = "open_poll")]
    [Description("Open a poll slide's round so the audience can answer, in a deck you can edit that a person has taken live (a join code is showing). Needs a poll with no round yet, or a drafted one, and no other slide's poll open. You can't reveal the counts, show the answer or start a new round: the presenter does. Returns the presenter's view of the poll: phase, round number, answered, joined, counts and settings.")]
    public Task<CallToolResult> OpenPoll(
        PollAgentAccess access,
        [Description("The deck's id.")] string deck,
        [Description("The poll slide's 1-based number, as get_deck gave it.")] int slide)
        => McpToolErrors.Run(() => access.OpenAsync(deck, slide));
}
