using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// The palette half of a LookView (L6): resolved tokens, so the client never looks a palette up by name.
public sealed record LookPaletteView(string Name, string Label, IReadOnlyDictionary<string, string> Tokens)
{
    public static LookPaletteView From(Palette palette) => new(palette.Name, palette.Label, palette.Tokens);
}
