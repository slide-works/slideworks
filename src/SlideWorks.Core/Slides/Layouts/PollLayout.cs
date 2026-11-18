using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class PollLayout
{
    public static LayoutDefinition Definition { get; } = new("poll", "Poll slide: a question and 2 to 8 options that the room answers on their phones while the deck is live. The poll's settings (how many picks, when results show, the correct answer, when it opens) are set with update_poll, never in the slots.", LayoutSurface.Light,
    [
        Req("question", new TextSlot(120)),
        Req("options", new ListSlot(new TextSlot(60), 2, 8), "Known by position: while a round is open they can be reworded or added to, not removed or reordered. Each option needs its own text: two that match, ignoring case and spaces at the ends, are refused"),
    ], rules: Rule);

    // A poll's options are told apart by their text on the phones, so two can't read the same (trimmed, ignoring case).
    private static void Rule(JsonElement slots, string path, SlotValidation validation)
    {
        var seen = new List<string>();
        foreach (var (option, index) in slots.GetProperty("options").EnumerateArray().Select((o, i) => (o.GetString()!, i)))
        {
            var text = option.Trim().ToLowerInvariant();
            if (seen.IndexOf(text) is var first and >= 0)
            {
                validation.Fail($"{path}.options[{index}]", $"the same text as options[{first}]; each option needs its own text");
                return;
            }
            seen.Add(text);
        }
    }
}
