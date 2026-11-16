using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Slides.Mcp;

[McpServerToolType]
public sealed class DeleteSlideTool
{
    [McpServerTool(Name = SlideEdits.DELETE_TOOL, Destructive = true)]
    [Description("Delete a slide from a deck you own. Later slides move up one and get a new revision, so read the deck again with get_deck before updating them. The deleted slide is kept on the server but no longer shows. Returns the deleted slide's number, how many slides are left and the new revision.")]
    public Task<CallToolResult> DeleteSlide(
        SlideEdits edits,
        [Description("The deck's id.")] string deck,
        [Description("The 1-based number of the slide to delete.")] int slide)
        => McpToolErrors.Run(() => edits.DeleteAsync(deck, slide));
}
