namespace SlideWorks.Core.Look.Manifest;

// An extra named colour of a custom palette ("extra-1" to "extra-4") and the text colour on it. Both are # plus 6
// hex digits.
public sealed record ManifestExtra
{
    public required string Name { get; init; }
    public required string Colour { get; init; }
    public required string Ink { get; init; }
}
