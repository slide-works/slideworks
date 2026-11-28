namespace SlideWorks.Core.Translations;

// One stored translation of a line (K2, D1): the translated text and the primary text it was
// translated from, so a change to the primary text marks it stale.
public sealed record TranslationEntry(string Text, string From);
