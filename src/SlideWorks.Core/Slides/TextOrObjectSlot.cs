using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// A plain string, or an object for the richer form: a bullet as "text" or {lead, text}, a table
// cell as "text" or {text, tone}.
public sealed class TextOrObjectSlot(TextSlot text, ObjectSlot richer) : ISlotType
{
    public TextSlot Text { get; } = text;

    public ObjectSlot Richer { get; } = richer;

    public string Expected => "text or an object";

    public string Kind => "text or object";

    public JsonObject ToJsonSchema() => new()
    {
        ["oneOf"] = new JsonArray(Text.ToJsonSchema(), Richer.ToJsonSchema()),
    };

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                Text.Validate(value, path, validation);
                break;
            case JsonValueKind.Object:
                Richer.Validate(value, path, validation);
                break;
            default:
                validation.Fail(path, $"expected {Expected}");
                break;
        }
    }
}
