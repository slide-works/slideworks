namespace SlideWorks.Core.Translations;

// How much of a deck one language covers (K3). Complete is Current * 100 / Lines rounded down, and 100
// when there are no lines.
public sealed record LanguageCoverage(int Lines, int Current, int Stale, int Missing, int Complete);
