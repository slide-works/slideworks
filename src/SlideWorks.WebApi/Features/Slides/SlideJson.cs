using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.WebApi.Features.Slides;

// How slots are stored in Slide.SlotsJson and read back.
public static class SlideJson
{
    public static JsonElement Slots(Slide slide)
    {
        using var document = JsonDocument.Parse(slide.SlotsJson);
        return document.RootElement.Clone();
    }

    // The slots as stored: SlideValidator treats a null optional field as absent, and the client's slot types have no
    // null, so every null-valued property is dropped at every depth. Expects slots that passed SlideValidator.
    public static string ForStorage(JsonElement slots)
    {
        var node = JsonNode.Parse(slots.GetRawText());
        DropNulls(node);
        return node?.ToJsonString() ?? "null";
    }

    private static void DropNulls(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in obj.Where(property => property.Value is null).Select(property => property.Key).ToList())
                {
                    obj.Remove(name);
                }
                foreach (var (_, value) in obj)
                {
                    DropNulls(value);
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    DropNulls(item);
                }
                break;
        }
    }
}
