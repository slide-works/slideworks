using SlideWorks.Core.Translations;

namespace SlideWorks.Core.Slides;

// The average glyph widths the fit rules wrap text with, in em: body text, headings and table cells.
// The built-in fonts are 0.56 each (see SlideFit); a template with its own fonts brings its own. Wide
// characters cost more than one average character through GlyphWidths, which scales with these widths.
public sealed record FitWidths(double Body, double Head, double Cell)
{
    public static FitWidths Default { get; } = new(0.56, 0.56, 0.56);

    // The widths text in a language is fitted with (K3): the template's own for a script with no fitEm
    // (Latin), else the script's fitEm for body, head and cell alike. A grapheme of such a script costs
    // one average character in GlyphWidths, so it costs 1.0 × fitEm. The code must be in the catalogue.
    public static FitWidths ForScript(FitWidths template, string languageCode)
    {
        var script = LanguageCatalogue.ScriptOf(languageCode)
            ?? throw new ArgumentException($"'{languageCode}' is not a supported language", nameof(languageCode));
        return script.FitEm is { } em ? new FitWidths(em, em, em) : template;
    }
}
