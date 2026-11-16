using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One layout in list_layouts: its name for add_slide, what it is for, whether it is a dark slide, the list whose items
// can appear one per step (null when the layout has no steps), and the JSON Schema its slots must match.
public sealed record LayoutInfo(string Name, string Description, bool Dark, string? StepsFrom, JsonObject Schema)
{
    public static LayoutInfo From(LayoutDefinition layout)
        => new(layout.Name, layout.Description, layout.Dark, layout.StepsFrom, layout.ToJsonSchema());
}
