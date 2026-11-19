namespace SlideWorks.Core.Slides;

// One piece of an overflowing region: a slot item, or the callout that shares the region, and its height in px.
public readonly record struct FitPart(string Name, double Height);
