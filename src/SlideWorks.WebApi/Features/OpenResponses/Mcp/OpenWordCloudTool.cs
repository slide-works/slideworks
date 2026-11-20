using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.OpenResponses.Mcp;

[McpServerToolType]
public sealed class OpenWordCloudTool
{
    public const string NAME = "open_word_cloud";

    // TODO: let an agent with edit access run it only while the deck is live, and refuse a slide of the other kind (D7).
    [McpServerTool(Name = NAME)]
    [Description("Open the word-cloud slide's round in a deck that is live, so guests can send words. Opening an open round changes nothing. Returns the slide's control view. It cannot reveal, hide, restore or start a new round.")]
    public Task<CallToolResult> Open(
        OpenResponseControls controls,
        [Description("The deck's id.")] string deck,
        [Description("The word-cloud slide's 1-based number.")] int slide)
        => McpToolErrors.Run(() => controls.OpenAsync(deck, slide));
}
