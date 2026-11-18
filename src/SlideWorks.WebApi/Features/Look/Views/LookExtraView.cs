using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Look.Views;

// A named colour a template adds to its palette, in a LookView (T7), with the ink that reads on it.
public sealed record LookExtraView(string Name, string Colour, string Ink)
{
    public static LookExtraView From(ExtraColour extra) => new(extra.Name, extra.Colour, extra.Ink);
}
