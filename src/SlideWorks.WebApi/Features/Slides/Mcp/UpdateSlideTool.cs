using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlideWorks.WebApi.Infrastructure.Mcp;

namespace SlideWorks.WebApi.Features.Slides.Mcp;

// surface comes in as raw JSON so that a null (clear it) differs from leaving it out (keep it).
[McpServerToolType]
public sealed class UpdateSlideTool
{
    [McpServerTool(Name = SlideEdits.UPDATE_TOOL)]
    [Description("Replace a slide's slots in a deck you own, and optionally its layout or part. Pass the revision get_deck gave for the slide: if the slide changed since (edited, moved or renumbered), the update is refused and you read the deck again. The slots are checked like add_slide's, and a slide that can't render is refused with an error naming the field. Returns the slide's number, steps, new revision, url and warnings: a layout outside the deck's template is kept and still renders, with a warning.")]
    public Task<CallToolResult> UpdateSlide(
        SlideEdits edits,
        [Description("The deck's id.")] string deck,
        [Description("The slide's 1-based number, as get_deck gave it.")] int slide,
        [Description("The slide's revision, as get_deck gave it.")] long revision,
        [Description("The slide's complete new slots as a JSON object; they replace the old ones.")] JsonObject slots,
        [Description("A new layout's name, or leave it out to keep the slide's layout.")] string? layout = null,
        [Description("A new 1-based part, or leave it out to keep the slide's part.")] int? part = null,
        [Description("true marks the slide as a deep dive, false clears the mark; leave it out to keep it. A deep dive shows the deep-dive mark and its notes read with a skip-if-short-of-time prefix; the stored notes never change.")] bool? deep_dive = null,
        [Description("\"light\" or \"dark\", for a layout whose surface is 'either' (list_layouts says which); null clears it, back to the layout's own surface; leave it out to keep the slide's surface. A surface the slide's layout cannot take is refused, so send null with a change to a layout that cannot take the stored one.")] JsonElement? surface = null)
        => McpToolErrors.Run(() => edits.UpdateAsync(new UpdateSlideRequest(deck, slide, revision, JsonSerializer.SerializeToElement(slots), layout, part, deep_dive, UpdateSlideRequest.SurfaceOf(surface))));
}
