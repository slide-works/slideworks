using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.OpenResponses.Mcp;

[McpServerToolType]
public sealed class CloseWordCloudTool
{
    public const string NAME = "close_word_cloud";

    // TODO: let an agent with edit access run it only while the deck is live, and refuse a slide of the other kind (D7).
    [McpServerTool(Name = NAME)]
    [Description("Close the word-cloud slide's open round in a deck that is live. Its words are kept. Returns the slide's control view. It cannot reveal, hide, restore or start a new round.")]
    public Task<CallToolResult> Close(
        OpenResponseControls controls,
        [Description("The deck's id.")] string deck,
        [Description("The word-cloud slide's 1-based number.")] int slide)
        => McpToolErrors.Run(() => controls.CloseAsync(deck, slide));
}
