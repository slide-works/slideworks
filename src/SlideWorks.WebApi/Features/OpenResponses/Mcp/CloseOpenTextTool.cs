using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.OpenResponses.Mcp;

[McpServerToolType]
public sealed class CloseOpenTextTool
{
    public const string NAME = "close_open_text";

    [McpServerTool(Name = NAME)]
    [Description("Close the open-text slide's open round in a deck you can edit that a person has taken live. Its answers are kept. A round that is not open is refused with wrong_phase. Returns the slide's control view. It cannot hide, restore or start a new round.")]
    public Task<CallToolResult> Close(
        OpenResponseControls controls,
        [Description("The deck's id.")] string deck,
        [Description("The open-text slide's 1-based number.")] int slide)
        => McpToolErrors.Run(() => controls.CloseAsAgentAsync(deck, slide, OpenResponseKind.OpenText));
}
