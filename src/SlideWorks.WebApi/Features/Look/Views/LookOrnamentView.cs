namespace SlideWorks.WebApi.Features.Look.Views;

// The ornament's colours in a LookView (T7), as token names. Empty means the colours the ornament draws with today.
public sealed record LookOrnamentView(IReadOnlyList<string> Colours);
