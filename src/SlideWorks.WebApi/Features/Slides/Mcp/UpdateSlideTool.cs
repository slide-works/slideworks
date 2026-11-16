using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Slides.Mcp;

[McpServerToolType]
public sealed class UpdateSlideTool
{
    [McpServerTool(Name = SlideEdits.UPDATE_TOOL)]
    [Description("Replace a slide's slots in a deck you own, and optionally its layout or part. Pass the revision get_deck gave for the slide: if the slide changed since (edited, moved or renumbered), the update is refused and you read the deck again. The slots are checked like add_slide's, and a slide that can't render is refused with an error naming the field. Returns the slide's number, steps, new revision and url.")]
    public Task<CallToolResult> UpdateSlide(
        SlideEdits edits,
        [Description("The deck's id.")] string deck,
        [Description("The slide's 1-based number, as get_deck gave it.")] int slide,
        [Description("The slide's revision, as get_deck gave it.")] long revision,
        [Description("The slide's complete new slots as a JSON object; they replace the old ones. Required.")] JsonElement? slots = null,
        [Description("A new layout's name, or leave it out to keep the slide's layout.")] string? layout = null,
        [Description("A new 1-based part, or leave it out to keep the slide's part.")] int? part = null)
        => McpToolErrors.Run(() => edits.UpdateAsync(new UpdateSlideRequest(deck, slide, revision, slots, layout, part)));
}
