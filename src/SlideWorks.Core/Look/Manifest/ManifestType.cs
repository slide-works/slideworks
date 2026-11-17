namespace SlideWorks.Core.Look.Manifest;

// A manifest's type: head and body font family names, the heading weight and its letter spacing.
public sealed record ManifestType
{
    public required string Head { get; init; }
    public required string Body { get; init; }
    public required int HeadWeight { get; init; }
    public required string HeadTracking { get; init; }
}
