namespace SlideWorks.WebApi.Features.Look.Views;

// The palette's closest contrast pair (L6): the pair with the least headroom over its bar.
public sealed record ContrastLowestView(string Pair, double Ratio, double Bar);
