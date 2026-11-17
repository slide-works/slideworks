namespace SlideWorks.Core.Look.CustomLayouts;

// A CSS variable an element sets from a number slot: data-var="--v: share" is ("--v", "share").
public sealed record TreeVar(string Name, string Slot);
