using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Slides.Mcp;

[McpServerToolType]
public sealed class MoveSlideTool
{
    [McpServerTool(Name = SlideEdits.MOVE_TOOL)]
    [Description("Move a slide in a deck you own to another number. The slides in between shift by one, and every slide that moves gets a new revision, so read the deck again with get_deck before updating them. Returns the slide's new number, the new revision and its url.")]
    public Task<CallToolResult> MoveSlide(
        SlideEdits edits,
        [Description("The deck's id.")] string deck,
        [Description("The slide's current 1-based number.")] int slide,
        [Description("The 1-based number the slide moves to.")] int to)
        => McpToolErrors.Run(() => edits.MoveAsync(deck, slide, to));
}
