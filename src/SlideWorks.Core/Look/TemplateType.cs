namespace SlideWorks.Core.Look;

// A template's type (L1): head and body font families, the heading weight and its letter spacing.
public sealed record TemplateType
{
    public required string Head { get; init; }
    public required string Body { get; init; }
    public required int HeadWeight { get; init; }
    public required string HeadTracking { get; init; }
}
