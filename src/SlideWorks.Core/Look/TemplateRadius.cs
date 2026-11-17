namespace SlideWorks.Core.Look;

// A template's corner radii in px, the values of --sw-radius-sm/md/lg/xl.
public sealed record TemplateRadius
{
    public required int Sm { get; init; }
    public required int Md { get; init; }
    public required int Lg { get; init; }
    public required int Xl { get; init; }
}
