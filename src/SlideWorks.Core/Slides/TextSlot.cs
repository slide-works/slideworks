using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// Plain text (D12): never Markdown or HTML. Line breaks only where the layout allows them.
public sealed class TextSlot(int maxLength, bool lineBreaks = false) : ISlotType
{
    public int MaxLength { get; } = maxLength;

    public bool LineBreaks { get; } = lineBreaks;

    public string Expected => "text";

    public string Kind => "text";

    public JsonObject ToJsonSchema()
    {
        var schema = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = MaxLength };
        if (!LineBreaks)
        {
            schema["pattern"] = "^[^\\r\\n]*$";
        }

        return schema;
    }

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        var text = value.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            validation.Fail(path, "must not be empty");
            return;
        }

        if (!LineBreaks && text.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            validation.Fail(path, "line breaks are not allowed here");
            return;
        }

        var length = new StringInfo(text).LengthInTextElements;
        if (length > MaxLength)
        {
            validation.Fail(path, $"at most {MaxLength} characters (got {length})");
        }
    }
}
