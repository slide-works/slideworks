namespace SlideWorks.Core.Look;

// A value a look change sets. A null Change means "leave it"; a Change whose Value is null resets it
// (a chrome key back to the template's default).
public sealed record Change<T>(T Value);
