using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One layout list_layouts answers: its definition, and for a custom layout the key its capacity is cached under
// (template, version and asset id), which is also what marks it custom.
public sealed record ListedLayout(LayoutDefinition Definition, string? CustomKey = null)
{
    public bool Custom => CustomKey is not null;
}
