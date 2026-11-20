using System.Text.Json;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Slides.Layouts;

public static class PollLayout
{
    public static LayoutDefinition Definition { get; } = new("poll", "Poll slide: a question the room answers on their phones while the deck is live. A choice poll has 2 to 8 options. An open question (poll.answers \"text\" or \"cloud\" on add_slide) has none: send an empty options list. Settings (how many picks, when results show, the correct answer, when it opens, text or cloud) go on add_slide's poll object, or later with update_poll. They are never slots.", LayoutSurface.Light,
    [
        Req("question", new TextSlot(120)),
        Req("options", new ListSlot(new TextSlot(60), 0, 8), "2 to 8 for a choice poll. [] for an open question (poll.answers text or cloud). Known by position: while a round is open they can be reworded or added to, not removed or reordered. Each option needs its own text: two that match, ignoring case and spaces at the ends, are refused"),
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
