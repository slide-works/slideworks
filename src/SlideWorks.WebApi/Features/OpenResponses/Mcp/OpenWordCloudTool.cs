using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.OpenResponses.Mcp;

[McpServerToolType]
public sealed class OpenWordCloudTool
{
    public const string NAME = "open_word_cloud";

    [McpServerTool(Name = NAME)]
    [Description("Open the word-cloud slide's round in a deck you can edit that a person has taken live, so guests can send words. Opening an open round changes nothing; a closed round stays closed until the presenter starts a new one. Returns the slide's control view. It cannot hide, restore or start a new round.")]
    public Task<CallToolResult> Open(
        OpenResponseControls controls,
        [Description("The deck's id.")] string deck,
        [Description("The word-cloud slide's 1-based number.")] int slide)
        => McpToolErrors.Run(() => controls.OpenAsAgentAsync(deck, slide, OpenResponseKind.WordCloud));
}
