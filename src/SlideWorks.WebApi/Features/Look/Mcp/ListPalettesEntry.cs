using System.Text.Json.Serialization;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Look.Mcp;

// One palette in list_palettes (L7): the words it answers to, its swatch, every token and its contrast summary. is_default
// marks the palette a new deck gets when neither it nor its template names one.
public sealed record ListPalettesEntry(
    string Name,
    string Label,
    [property: JsonPropertyName("is_default")] bool IsDefault,
    IReadOnlyList<string> Words,
    IReadOnlyList<string> Swatch,
    IReadOnlyDictionary<string, string> Tokens,
    ContrastView Contrast)
{
    public static ListPalettesEntry From(Palette palette)
        => new(palette.Name, palette.Label, palette.IsDefault, palette.Words, palette.Swatch, palette.Tokens, ContrastView.From(palette));
}
