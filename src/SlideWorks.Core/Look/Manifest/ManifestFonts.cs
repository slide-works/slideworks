namespace SlideWorks.Core.Look.Manifest;

// One entry of a manifest's fonts[]: an uploaded woff2 file and what it is for. Role is "head", "body" or "mono",
// Weight is 100 to 900 and Style is "normal" or "italic". The right to serve the font is kept on the stored file.
public sealed record ManifestFonts
{
    public required string Role { get; init; }
    public required string Family { get; init; }
    public required string File { get; init; }
    public required int Weight { get; init; }
    public required string Style { get; init; }
}
