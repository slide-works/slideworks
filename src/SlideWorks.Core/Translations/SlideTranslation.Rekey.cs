using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Translations;

public static partial class SlideTranslation
{
    // Moves one language's entries onto a slide's new slots (D13), in this order of precedence:
    // 1. an entry that is current at its own path stays there;
    // 2. an entry whose From equals the text of a line no entry holds yet moves to that line (the first such line, in
    //    document order), so a reordered or moved item keeps its translation;
    // 3. an entry whose path is still a line no entry holds yet stays there, and is stale when the text changed;
    // 4. every other entry is removed.
    // Entries are taken in the order they are stored.
    public static RekeyResult Rekey(LayoutDefinition layout, JsonElement slots, IReadOnlyDictionary<string, TranslationEntry> entries)
    {
        var lines = TranslatableLines.Of(layout, slots);
        var placed = new Dictionary<string, TranslationEntry>(StringComparer.Ordinal);
        var waiting = new List<KeyValuePair<string, TranslationEntry>>();
        var textAt = lines.ToDictionary(line => line.Path, line => line.Text, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (textAt.TryGetValue(entry.Key, out var text) && string.Equals(text, entry.Value.From, StringComparison.Ordinal))
            {
                placed[entry.Key] = entry.Value;
            }
            else
            {
                waiting.Add(entry);
            }
        }

        var unmoved = new List<KeyValuePair<string, TranslationEntry>>();
        foreach (var entry in waiting)
        {
            var target = lines.FirstOrDefault(candidate => !placed.ContainsKey(candidate.Path) && string.Equals(candidate.Text, entry.Value.From, StringComparison.Ordinal));
            if (target is null)
            {
                unmoved.Add(entry);
            }
            else
            {
                placed[target.Path] = entry.Value;
            }
        }

        var removed = new List<string>();
        foreach (var entry in unmoved)
        {
            if (textAt.ContainsKey(entry.Key) && !placed.ContainsKey(entry.Key))
            {
                placed[entry.Key] = entry.Value;
            }
            else
            {
                removed.Add(entry.Key);
            }
        }

        var stale = lines
            .Where(line => placed.TryGetValue(line.Path, out var entry) && !string.Equals(entry.From, line.Text, StringComparison.Ordinal))
            .Select(line => line.Path)
            .ToList();
        var ordered = lines
            .Where(line => placed.ContainsKey(line.Path))
            .ToDictionary(line => line.Path, line => placed[line.Path], StringComparer.Ordinal);
        return new RekeyResult(ordered, stale, removed);
    }
}
