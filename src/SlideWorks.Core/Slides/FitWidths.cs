namespace SlideWorks.Core.Slides;

// The average glyph widths the fit rules wrap text with, in em: body text, headings and table cells.
// The built-in fonts are 0.56 each (see SlideFit); a template with its own fonts brings its own. Wide
// characters cost more than one average character through GlyphWidths, which scales with these widths.
public sealed record FitWidths(double Body, double Head, double Cell)
{
    public static FitWidths Default { get; } = new(0.56, 0.56, 0.56);
}
