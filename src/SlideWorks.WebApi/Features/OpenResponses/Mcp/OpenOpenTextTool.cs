using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.OpenResponses.Mcp;

[McpServerToolType]
public sealed class OpenOpenTextTool
{
    public const string NAME = "open_open_text";

    [McpServerTool(Name = NAME)]
    [Description("Open the open-text slide's round in a deck you can edit that a person has taken live, so guests can send answers. Opening an open round changes nothing; a closed round stays closed until the presenter starts a new one. Returns the slide's control view. It cannot hide, restore or start a new round.")]
    public Task<CallToolResult> Open(
        OpenResponseControls controls,
        [Description("The deck's id.")] string deck,
        [Description("The open-text slide's 1-based number.")] int slide)
        => McpToolErrors.Run(() => controls.OpenAsAgentAsync(deck, slide, OpenResponseKind.OpenText));
}
