using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class PracticeLayout
{
    public static LayoutDefinition Definition { get; } = new("practice", "A practice: what to do, what comes back and what to avoid, with an optional callout.", LayoutSurface.Light,
    [
        Opt("kicker", new TextSlot(KICKER)),
        Req("headline", new TextSlot(HEADLINE)),
        Req("do", new ListSlot(Obj(Req("lead", new TextSlot(50)), Req("text", new TextSlot(120))), 1, 5)),
        Req("comes_back", new ListSlot(new TextSlot(100), 0, 4), "What the team gets back; may be empty"),
        Req("avoid", new ListSlot(new TextSlot(100), 0, 4), "What to avoid; may be empty"),
        Opt("callout", new BlockSlot(InlineBlocks.Callout)),
    ], rules: Rule);

    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        // .practice: 1.15fr 1fr with a 48 px gap (873 and 759 px). Each column has an 18 px header
        // (34 px with its margin); an .it card is 44 px of padding plus 25 px/1.4 text, 14 px apart.
        var doCards = slots.GetProperty("do").EnumerateArray()
            .Sum(i => 58 + (SlideFit.Lines(SlideFit.Words(i), 821, 25, validation.Widths.Body) * 35));
        SlideFit.Check(34 + doCards, $"{path}.do", validation);

        var right = 0.0;
        foreach (var list in new[] { "comes_back", "avoid" }.Select(name => slots.GetProperty(name)).Where(l => l.GetArrayLength() > 0))
        {
            right += (right > 0 ? 26 : 0) + 34
                + list.EnumerateArray().Sum(i => 58 + (SlideFit.Lines(i.GetString()!, 699, 25, validation.Widths.Body) * 35));
        }

        if (ObjectSlot.IsPresent(slots, "callout", out var callout))
        {
            right += LayoutRules.CalloutHeight(callout, 759, validation.Widths) - 8;
        }

        SlideFit.Check(right, $"{path}.comes_back", validation);
    }
}
