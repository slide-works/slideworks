using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One layout in list_layouts: its name for add_slide, what it is for, whether it is an always-dark slide, the list whose
// items can appear one per step (null when the layout has no steps), and the JSON Schema its slots must match. Dark and
// StepsFrom stay for existing clients: Dark is true only for an always-dark layout (Surface covers the rest) and
// StepsFrom is the first list the steps reveal (Steps says it all). Surface is "light", "dark" or "either": with
// either, add_slide's surface picks. Steps is what the steps reveal, in words (null when the layout has no steps).
// Tracker says whether the layout is a tracker. Custom is true for a layout the deck's template brings.
public sealed record LayoutInfo(string Name, string Description, bool Dark, string? StepsFrom, JsonObject Schema, string Surface, string? Steps, bool Tracker, IReadOnlyList<SlotCapacity> Capacity, bool Custom = false)
{
    public static LayoutInfo From(ListedLayout listed)
    {
        var layout = listed.Definition;
        return new(layout.Name, layout.Description, layout.Dark, layout.StepsFrom, layout.ToJsonSchema(), SurfaceName(layout.Surface), layout.Steps?.Phrase, layout.Tracker, LayoutCapacity.Of(layout, listed.CustomKey), listed.Custom);
    }

    public static string SurfaceName(LayoutSurface surface) => surface switch
    {
        LayoutSurface.Light => "light",
        LayoutSurface.Dark => "dark",
        LayoutSurface.Either => "either",
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
    };
}
