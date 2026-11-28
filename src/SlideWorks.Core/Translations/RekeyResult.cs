namespace SlideWorks.Core.Translations;

// One language's entries after a slide's slots were replaced (SlideTranslation.Rekey, D13): the entries kept, under the paths
// they now belong to; the paths of the kept entries that are stale, in line order; and the old paths of the entries removed
// because no line holds them any more.
public sealed record RekeyResult(
    IReadOnlyDictionary<string, TranslationEntry> Entries,
    IReadOnlyList<string> Stale,
    IReadOnlyList<string> Removed);
