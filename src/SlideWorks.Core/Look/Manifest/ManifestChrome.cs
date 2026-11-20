namespace SlideWorks.Core.Look.Manifest;

// A manifest's chrome defaults. Footer is "author", "date" or "off"; TopBar is "none" or "accent".
public sealed record ManifestChrome
{
    public required bool Tracker { get; init; }
    public required string Footer { get; init; }
    public required bool Progress { get; init; }
    public required bool DeepDivesOnDivider { get; init; }
    public required bool Draft { get; init; }
    public required string TopBar { get; init; }
}
