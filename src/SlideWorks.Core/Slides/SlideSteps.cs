using System.Text.Json;

namespace SlideWorks.Core.Slides;

// How many steps a slide has (K2). A layout with a reveal list has one step per item when reveal is
// "one-by-one". Every other slide has none. Expects slots that passed SlideValidator.
public static class SlideSteps
{
    public static int Count(string layout, JsonElement slots)
    {
        var list = LayoutCatalogue.Find(layout)?.StepsFrom;
        if (list is null || slots.ValueKind != JsonValueKind.Object
            || !slots.TryGetProperty("reveal", out var reveal) || reveal.ValueKind != JsonValueKind.String
            || !string.Equals(reveal.GetString(), "one-by-one", StringComparison.Ordinal)
            || !slots.TryGetProperty(list, out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return items.GetArrayLength();
    }
}
