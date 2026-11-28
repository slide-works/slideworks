namespace SlideWorks.Core.Translations;

// A script's fallback font and its fit width in em per grapheme (K1). A null FitEm means the template's
// own FitWidths apply.
public sealed record LanguageScript(string? Font, double? FitEm);
