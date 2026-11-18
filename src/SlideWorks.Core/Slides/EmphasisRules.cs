using System.Text.Json;

namespace SlideWorks.Core.Slides;

// The emphasis rule every layout with an emphasis slot runs after its schema (K10): each phrase occurs
// exactly once in the text it is in, a text that is not set cannot be named, and two phrases in one text
// do not overlap. Without 'in', exactly one of the layout's emphasis texts must hold the phrase.
public static class EmphasisRules
{
    public const string SLOT = "emphasis";

    public static void Check(JsonElement slots, string path, SlotValidation validation, params string[] targets)
    {
        if (slots.ValueKind != JsonValueKind.Object
            || !ObjectSlot.IsPresent(slots, SLOT, out var entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var texts = targets
            .Select(target => (Target: target, Text: TextAt(slots, target)))
            .Where(t => t.Text is not null)
            .ToDictionary(t => t.Target, t => t.Text!, StringComparer.Ordinal);
        var placed = new List<Span>();
        var index = 0;

        // The schema has refused a list of more than three; this keeps the walk short whatever was sent.
        foreach (var entry in entries.EnumerateArray().Take(EmphasisSlot.MAX_ENTRIES))
        {
            var span = Place(entry, $"{path}.{SLOT}[{index}]", index, texts, targets, validation);
            if (span is not null)
            {
                var other = placed.FirstOrDefault(p => p.Overlaps(span));
                if (other is not null)
                {
                    validation.Fail($"{path}.{SLOT}[{index}].text", $"'{EchoText.Of(span.Phrase)}' overlaps {SLOT}[{other.Index}]");
                }

                placed.Add(span);
            }

            index++;
        }
    }

    // Where one entry's phrase sits, or null when it has no single place (which is then a failure, unless
    // the entry is not the shape the schema asks for: the schema has said so already).
    private static Span? Place(
        JsonElement entry,
        string entryPath,
        int index,
        Dictionary<string, string> texts,
        string[] targets,
        SlotValidation validation)
    {
        if (entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty("text", out var phraseValue)
            || phraseValue.ValueKind != JsonValueKind.String
            || phraseValue.GetString() is not { Length: > 0 } phrase)
        {
            return null;
        }

        var echo = EchoText.Of(phrase);
        string target;
        if (ObjectSlot.IsPresent(entry, "in", out var inValue))
        {
            if (inValue.ValueKind != JsonValueKind.String || !targets.Contains(inValue.GetString(), StringComparer.Ordinal))
            {
                return null;
            }

            target = inValue.GetString()!;
            if (!texts.ContainsKey(target))
            {
                validation.Fail($"{entryPath}.in", $"{target} is not set");
                return null;
            }
        }
        else
        {
            var holders = texts.Keys.Where(t => texts[t].Contains(phrase, StringComparison.Ordinal)).ToList();
            if (holders.Count == 0)
            {
                validation.Fail($"{entryPath}.text", $"'{echo}' is not in {string.Join(" or ", texts.Count > 0 ? texts.Keys : targets)}");
                return null;
            }

            if (holders.Count > 1)
            {
                validation.Fail($"{entryPath}.text", $"'{echo}' is in {string.Join(" and ", holders)}; say which with 'in'");
                return null;
            }

            target = holders[0];
        }

        var text = texts[target];
        var occurrences = Occurrences(text, phrase);
        if (occurrences == 0)
        {
            validation.Fail($"{entryPath}.text", $"'{echo}' is not in {target}");
            return null;
        }

        if (occurrences > 1)
        {
            validation.Fail($"{entryPath}.text", $"'{echo}' occurs {occurrences} times in {target}; emphasis needs a phrase that occurs once");
            return null;
        }

        return new Span(index, target, phrase, text.IndexOf(phrase, StringComparison.Ordinal));
    }

    // How many times the phrase occurs, counting occurrences that overlap each other ('aa' is twice in 'aaa').
    private static int Occurrences(string text, string phrase)
    {
        var count = 0;
        for (var at = text.IndexOf(phrase, StringComparison.Ordinal); at >= 0; at = text.IndexOf(phrase, at + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    // The text at a dotted path from the slots, or null when it is not set (or is not text).
    private static string? TextAt(JsonElement slots, string target)
    {
        var current = slots;
        foreach (var name in target.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !ObjectSlot.IsPresent(current, name, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private sealed record Span(int Index, string Target, string Phrase, int Start)
    {
        public bool Overlaps(Span other) =>
            string.Equals(Target, other.Target, StringComparison.Ordinal)
            && Start < other.Start + other.Phrase.Length
            && other.Start < Start + Phrase.Length;
    }
}
