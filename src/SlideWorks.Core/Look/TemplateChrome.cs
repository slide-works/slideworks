namespace SlideWorks.Core.Look;

// A template's chrome defaults (L1). Footer is "author", "date" or "off".
public sealed record TemplateChrome
{
    public required bool Tracker { get; init; }
    public required string Footer { get; init; }
    public required bool Progress { get; init; }
    public required bool DeepDivesOnDivider { get; init; }
    public required bool Draft { get; init; }
}
