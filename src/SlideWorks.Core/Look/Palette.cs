namespace SlideWorks.Core.Look;

// A palette (L1): every colour, never a font. Tokens maps each --sw-<key> custom property, without
// its prefix, to a colour: # and six hex digits in a built-in. accent-2 equals accent in a built-in. The built-ins live in src/contracts/palettes.json.
public sealed record Palette
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required bool IsDefault { get; init; }
    public required IReadOnlyList<string> Words { get; init; }
    public required IReadOnlyList<string> Swatch { get; init; }
    public required IReadOnlyDictionary<string, string> Tokens { get; init; }
}
