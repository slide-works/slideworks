using System.Text.Json;

namespace SlideWorks.Core.Slides;

// What a layout reveals step by step (K16): an ordered list of sources, each counting its steps
// from the slots and saying so in a phrase for the docs. Sources count only when the slide's reveal
// is on. A callout that reveals last adds one step whatever reveal is.
public sealed class StepPlan
{
    private const string WHEN_ON = "when `reveal` is `one-by-one`";

    private readonly IReadOnlyList<Source> _sources;

    private StepPlan(IReadOnlyList<Source> sources)
    {
        _sources = sources;
    }

    // The plan in words, for the layout docs and list_layouts.
    public string Phrase => string.Join(", then ", _sources.Select(s => s.Words));

    // The first list the plan reveals item by item, or null when it has none.
    public string? FirstList => _sources.Select(s => s.ListName).FirstOrDefault(l => l is not null);

    // One step per item of the list.
    public static StepPlan List(string field) =>
        Of(field, slots => ItemCount(slots, field), $"one per `{field}` item {WHEN_ON}");

    // One step per item of the list, the last item first.
    public static StepPlan ListReversed(string field) =>
        Of(field, slots => ItemCount(slots, field), $"one per `{field}` item, last first, {WHEN_ON}");

    // That many steps, whatever the slots hold.
    public static StepPlan Fixed(int count, string phrase) =>
        Of(null, _ => count, $"{count} for {phrase} {WHEN_ON}");

    // That many steps when the slot is there. The slot is a dotted path, such as "failure.stamp".
    public static StepPlan When(string slot, int count, string phrase) =>
        Of(null, slots => IsSet(slots, slot) ? count : 0, $"{count} for {phrase} when `{slot}` is set and `reveal` is `one-by-one`");

    // That many steps when any one of the slots is there.
    public static StepPlan WhenAny(IReadOnlyList<string> slots, int count, string phrase)
    {
        var names = string.Join(" or ", slots.Select(s => $"`{s}`"));
        return Of(null, element => slots.Any(s => IsSet(element, s)) ? count : 0, $"{count} for {phrase} when {names} is set and `reveal` is `one-by-one`");
    }

    // One for a callout whose reveal is "last", else none. It counts whatever the slide's reveal is.
    public static int CalloutStep(JsonElement slots) =>
        Find(slots, "callout.reveal") is { ValueKind: JsonValueKind.String } reveal
            && string.Equals(reveal.GetString(), "last", StringComparison.Ordinal) ? 1 : 0;

    // This plan's steps, then the next one's.
    public StepPlan Then(StepPlan next) => new([.. _sources, .. next._sources]);

    public int Count(JsonElement slots) =>
        (IsOn(slots) ? _sources.Sum(s => s.CountOf(slots)) : 0) + CalloutStep(slots);

    private static StepPlan Of(string? list, Func<JsonElement, int> count, string phrase) => new([new Source(list, count, phrase)]);

    // "strike-and-answer" is the table's second way of revealing one row per step.
    private static bool IsOn(JsonElement slots) =>
        Find(slots, "reveal") is { ValueKind: JsonValueKind.String } reveal
            && reveal.GetString() is "one-by-one" or "strike-and-answer";

    private static int ItemCount(JsonElement slots, string field) =>
        Find(slots, field) is { ValueKind: JsonValueKind.Array } items ? items.GetArrayLength() : 0;

    private static bool IsSet(JsonElement slots, string path) => Find(slots, path) is not null;

    // The value at a dotted path, or null when a part of it is absent or JSON null.
    private static JsonElement? Find(JsonElement slots, string path)
    {
        var current = slots;
        foreach (var name in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Null ? null : current;
    }

    private sealed record Source(string? ListName, Func<JsonElement, int> CountOf, string Words);
}
