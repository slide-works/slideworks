namespace SlideWorks.Core.Look;

// A template's shape (L1): corner radii in px and the card shadow (a CSS box-shadow).
public sealed record TemplateShape
{
    public required TemplateRadius Radius { get; init; }
    public required string Shadow { get; init; }
}
