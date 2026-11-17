using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Look.Views;

// One palette in GET /api/looks (looks.json), with its contrast summary.
public sealed record LooksPaletteView(
    string Name,
    string Label,
    bool IsDefault,
    IReadOnlyList<string> Swatch,
    IReadOnlyDictionary<string, string> Tokens,
    ContrastView Contrast)
{
    public static LooksPaletteView From(Palette palette)
        => new(palette.Name, palette.Label, palette.IsDefault, palette.Swatch, palette.Tokens, ContrastView.From(palette));
}
