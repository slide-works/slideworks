using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Presenting.Mcp;

// set_talk_length (P7): the same TalkLengthCommands path as PUT /api/decks/{id}/talk-length, so the rules and the error
// texts match. minutes has no default, so it is required: null clears the talk length, and a missing key is an error.
[McpServerToolType]
public sealed class SetTalkLengthTool
{
    [McpServerTool(Name = TalkLengthCommands.TOOL)]
    [Description("Set how long the talk is, in minutes (1 to 600), or pass null to clear it. The presenter's timer shows the time left against it. You must own the deck. The change shows in the deck's activity.")]
    public Task<CallToolResult> SetTalkLength(
        TalkLengthCommands commands,
        [Description("The deck's id.")] string deck_id,
        [Description("The talk length in whole minutes, 1 to 600, or null to clear it.")] int? minutes)
        => McpToolErrors.Run(async () =>
        {
            await commands.SetAsync(deck_id, minutes, TalkLengthCommands.TOOL);
            return new SetTalkLengthResult(minutes);
        });
}
