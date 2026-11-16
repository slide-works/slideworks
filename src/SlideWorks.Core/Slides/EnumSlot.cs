using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

public sealed class EnumSlot(params string[] values) : ISlotType
{
    public IReadOnlyList<string> Values { get; } = values;

    public string Expected => "one of " + string.Join(", ", Values.Select(v => $"'{v}'"));

    public string Kind => string.Join(" | ", Values);

    public JsonObject ToJsonSchema() => new()
    {
        ["type"] = "string",
        ["enum"] = new JsonArray(Values.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()),
    };

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        var text = value.GetString();
        if (!Values.Contains(text, StringComparer.Ordinal))
        {
            validation.Fail(path, $"expected {Expected} (got '{text}')");
        }
    }
}
