using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Slides;

// update_slide's arguments (K6, L7). Slide is the 1-based number and Revision the one get_deck gave for it. A null Layout,
// Part or DeepDive keeps the slide's own. A missing or null slots never gets here: McpArgumentCheck refuses it on the wire
// as "slots: required". A null Surface keeps the slide's own; a Change holding null clears it, back to the layout's own.
public sealed record UpdateSlideRequest(string Deck, int Slide, long Revision, JsonElement Slots, string? Layout, int? Part, bool? DeepDive = null, Change<string?>? Surface = null)
{
    // The surface as the tool got it: left out keeps the slide's own, a JSON null clears it, and a word sets it.
    public static Change<string?>? SurfaceOf(JsonElement? given) => given?.ValueKind switch
    {
        null or JsonValueKind.Undefined => null,
        JsonValueKind.Null => new Change<string?>(null),
        JsonValueKind.String => new Change<string?>(given.Value.GetString()),
        _ => throw new BadRequestException($"surface: use '{SlideSurface.LIGHT}' or '{SlideSurface.DARK}'"),
    };
}
