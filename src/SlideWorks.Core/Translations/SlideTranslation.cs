using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Translations;

// Applies one language's entries to a slide's slots (K3, D1, D18). A current entry (its From is the
// line's text) replaces the leaf; a missing or stale line keeps the primary text and counts as a
// fallback; an entry whose path is not a line is ignored. With dropUnmatchedEmphasis, an emphasis whose
// phrase is no longer in its merged text is dropped, so the slide renders without that highlight.
// The server's TranslationMerge.For is the only caller outside translate_slides; nothing else merges.
public static class SlideTranslation
{
    public static MergedSlide Merge(
        LayoutDefinition layout,
        JsonElement slots,
        IReadOnlyDictionary<string, TranslationEntry>? entries,
        bool dropUnmatchedEmphasis = true)
    {
        var lines = TranslatableLines.Of(layout, slots);
        var views = new List<LineView>(lines.Count);
        var merged = JsonNode.Parse(slots.GetRawText())!;
        var changed = false;
        var fallback = 0;
        var emphasisPrefix = layout.Slots.Fields.FirstOrDefault(f => f.Type is EmphasisSlot) is { } emphasis ? "/" + emphasis.Name + "/" : null;
        foreach (var line in lines)
        {
            TranslationEntry? entry = null;
            entries?.TryGetValue(line.Path, out entry);
            var state = StateOf(line, entry);
            if (state == LineState.Current)
            {
                SlotPointer.Replace(merged, line.Path, entry!.Text);
                changed = true;
            }
            else if (emphasisPrefix is null || !line.Path.StartsWith(emphasisPrefix, StringComparison.Ordinal))
            {
                // D24: an unmatched emphasis is dropped and shows no English, so it is not a fallback line.
                fallback++;
            }

            views.Add(new LineView(line.Path, line.Text, entry?.Text, state));
        }

        if (dropUnmatchedEmphasis)
        {
            changed |= DropUnmatchedEmphasis(layout, merged);
        }

        return new MergedSlide(changed ? JsonSerializer.SerializeToElement(merged) : slots.Clone(), fallback, views);
    }

    private static LineState StateOf(SlotLine line, TranslationEntry? entry)
    {
        if (entry is null)
        {
            return LineState.Missing;
        }

        return string.Equals(entry.From, line.Text, StringComparison.Ordinal) ? LineState.Current : LineState.Stale;
    }

    // Emphasis is a top-level field of the layout (SlotKit.Emphasis). An entry with 'in' must be in that
    // text; without it, in any of the layout's emphasis texts. Returns true when anything was dropped.
    private static bool DropUnmatchedEmphasis(LayoutDefinition layout, JsonNode merged)
    {
        var field = layout.Slots.Fields.FirstOrDefault(f => f.Type is EmphasisSlot);
        if (field is null || merged is not JsonObject slots || slots[field.Name] is not JsonArray entries)
        {
            return false;
        }

        var targets = ((EmphasisSlot)field.Type).Targets;
        var dropped = false;
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index] is JsonObject entry
                && TextOf(entry["text"]) is { } phrase
                && !TargetsOf(entry, targets).Any(target => TextAt(slots, target)?.Contains(phrase, StringComparison.Ordinal) == true))
            {
                entries.RemoveAt(index);
                dropped = true;
            }
        }

        if (dropped && entries.Count == 0)
        {
            slots.Remove(field.Name);
        }

        return dropped;
    }

    private static IEnumerable<string> TargetsOf(JsonObject entry, IReadOnlyList<string> targets) =>
        TextOf(entry["in"]) is { } named ? [named] : targets;

    // The text at a dotted target path ('quote.text'), or null when it is not set.
    private static string? TextAt(JsonObject slots, string target)
    {
        JsonNode? current = slots;
        foreach (var name in target.Split('.'))
        {
            current = current is JsonObject shape ? shape[name] : null;
        }

        return TextOf(current);
    }

    private static string? TextOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;
}
