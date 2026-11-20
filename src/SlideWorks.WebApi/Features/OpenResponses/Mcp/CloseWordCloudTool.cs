using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.OpenResponses.Mcp;

[McpServerToolType]
public sealed class CloseWordCloudTool
{
    public const string NAME = "close_word_cloud";

    [McpServerTool(Name = NAME)]
    [Description("Close the word-cloud slide's open round in a deck you can edit that a person has taken live. Its words are kept. A round that is not open is refused with wrong_phase. Returns the slide's control view. It cannot hide, restore or start a new round.")]
    public Task<CallToolResult> Close(
        OpenResponseControls controls,
        [Description("The deck's id.")] string deck,
        [Description("The word-cloud slide's 1-based number.")] int slide)
        => McpToolErrors.Run(() => controls.CloseAsAgentAsync(deck, slide, OpenResponseKind.WordCloud));
}
