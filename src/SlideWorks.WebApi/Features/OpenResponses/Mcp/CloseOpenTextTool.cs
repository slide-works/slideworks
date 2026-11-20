using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.OpenResponses.Mcp;

[McpServerToolType]
public sealed class CloseOpenTextTool
{
    public const string NAME = "close_open_text";

    // TODO: let an agent with edit access run it only while the deck is live, and refuse a slide of the other kind (D7).
    [McpServerTool(Name = NAME)]
    [Description("Close the open-text slide's open round in a deck that is live. Its answers are kept. Returns the slide's control view. It cannot reveal, hide, restore or start a new round.")]
    public Task<CallToolResult> Close(
        OpenResponseControls controls,
        [Description("The deck's id.")] string deck,
        [Description("The open-text slide's 1-based number.")] int slide)
        => McpToolErrors.Run(() => controls.CloseAsync(deck, slide));
}
