using SlideWorks.Core.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One block in a compact list_layouts, stated once under blocks.<name>: its slots. A block that is one value (a chip) has one slot named after it; journey, which agents never set, has none.
public sealed record CompactBlockInfo(IReadOnlyList<CompactSlot> Slots)
{
    public const string EMPHASIS = "emphasis";

    public static CompactBlockInfo From(InlineBlock block) => new(block.Type switch
    {
        null => [],
        ObjectSlot obj => CompactSlot.FieldsOf(obj.Fields),
        var type => [CompactSlot.From(block.Name, type, true)],
    });

    // Emphasis is a slot type of its own, not an inline block: a list of up to 3 {text, tone, in} entries.
    public static CompactBlockInfo Emphasis() => new(
    [
        new CompactSlot(
            EMPHASIS,
            "list",
            true,
            EmphasisSlot.MAX_ENTRIES,
            Item: new CompactSlot(
                null,
                "object",
                true,
                null,
                Slots:
                [
                    new CompactSlot("text", "text", true, EmphasisSlot.MAX_PHRASE),
                    new CompactSlot("tone", "enum", true, null, Values: ["accent", "positive", "negative"]),
                    new CompactSlot("in", "text", false, null),
                ])),
    ]);
}
