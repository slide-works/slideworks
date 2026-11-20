namespace SlideWorks.Core.Look.Manifest;

// A custom template's manifest (T3): the content of template.json, and of a stored template version. The format is
// src/contracts/template-manifest.schema.json. A file field holds an asset id when stored and on the wire, and a path
// in the zip inside a bundle; ManifestFiles is the only way to walk them.
public sealed record TemplateManifest
{
    // The short name: shown to people and agents, not unique, never an address.
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required string Purpose { get; init; }
    public required string BasedOn { get; init; }
    public required IReadOnlyList<string> Layouts { get; init; }
    public required ManifestChrome Chrome { get; init; }
    public required IReadOnlyList<string> Outline { get; init; }
    public required IReadOnlyList<string> HouseStyle { get; init; }
    public required ManifestType Type { get; init; }
    public required ManifestShape Shape { get; init; }
    public required IReadOnlyList<ManifestFonts> Fonts { get; init; }
    public required ManifestLogos Logos { get; init; }
    public required ManifestTitleArt TitleArt { get; init; }
    public required ManifestOrnament Ornament { get; init; }
    public ManifestPalette? Palette { get; init; }
    public required bool LockPalette { get; init; }
    public required IReadOnlyList<ManifestCustomLayout> CustomLayouts { get; init; }
}
