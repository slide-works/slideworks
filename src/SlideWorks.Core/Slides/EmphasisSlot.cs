using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// Which phrases of a layout's display text are drawn in a tone (K10): 1 to 3 entries, each a phrase that
// is already in the text, a tone, and the text slot it is in when the layout has more than one. The
// text slots stay exactly the text shown; EmphasisRules checks each phrase against them.
public sealed class EmphasisSlot : ISlotType
{
    public const int MIN_ENTRIES = 1;
    public const int MAX_ENTRIES = 3;
    public const int MAX_PHRASE = 60;

    private readonly ListSlot _entries;

    public EmphasisSlot(params string[] targets)
    {
        if (targets.Length == 0)
        {
            throw new ArgumentException("Emphasis needs at least one text slot to sit in", nameof(targets));
        }

        Targets = targets;
        _entries = new ListSlot(
            SlotKit.Obj(
                SlotKit.Req("text", new TextSlot(MAX_PHRASE), "A phrase that occurs exactly once in the text it is in"),
                SlotKit.Req("tone", new EnumSlot("accent", "positive", "negative"), SlotKit.TONE),
                SlotKit.Opt("in", new EnumSlot(targets), "The text slot the phrase is in; needed only when more than one holds it")),
            MIN_ENTRIES,
            MAX_ENTRIES);
    }

    // The text slots that may carry emphasis, as paths from the layout's slots ('headline', 'quote.text').
    public IReadOnlyList<string> Targets { get; }

    public string Expected => _entries.Expected;

    public string Kind => "emphasis";

    public JsonObject ToJsonSchema() => _entries.ToJsonSchema();

    public void Validate(JsonElement value, string path, SlotValidation validation) => _entries.Validate(value, path, validation);
}
