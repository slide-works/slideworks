using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

[McpServerToolType]
public sealed class GetDeckUrlTool
{
    [McpServerTool(Name = "get_deck_url", ReadOnly = true)]
    [Description("Get the player link for a deck you own, to share or open. Give a slide to link straight to it (#3), and a step as well to link to that point of its reveal (#3.2).")]
    public Task<CallToolResult> GetDeckUrl(
        DeckQueries queries,
        [Description("The deck's id.")] string deck,
        [Description("The 1-based slide number to open at.")] int? slide = null,
        [Description("The step of that slide to open at, from 0 (nothing revealed yet) to the slide's steps. Needs a slide.")] int? step = null)
        => McpToolErrors.Run(() => queries.UrlAsync(deck, slide, step));
}
