namespace SlideWorks.Core.Slides;

// What a fit check measured, whether or not it overflowed. Parts are the pieces that add up to Height.
// Shares are the slots that draw in the same region, so an error can name them and not only the path.
public sealed record FitRegion(
    string Path,
    double Height,
    double Budget,
    IReadOnlyList<FitPart> Parts,
    IReadOnlyList<string> Shares);
