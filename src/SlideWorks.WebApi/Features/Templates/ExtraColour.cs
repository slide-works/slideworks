namespace SlideWorks.WebApi.Features.Templates;

// A named colour a template adds to its palette (T4), with the ink that reads on it.
public sealed record ExtraColour(string Name, string Colour, string Ink);
