using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

public sealed class EnumSlot(params string[] values) : ISlotType
{
    public IReadOnlyList<string> Values { get; } = values;

    // Older names that stay valid for stored slides, each mapped to the value it means (L4, D15). Stored
    // values are never rewritten; a reader resolves an alias with Resolve.
    public IReadOnlyDictionary<string, string> Aliases { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    // The value an optional slot means when it is left out, or null when leaving it out means none.
    public string? Default { get; init; }

    public string Resolve(string value) => Aliases.TryGetValue(value, out var canonical) ? canonical : value;

    public string Expected => "one of " + string.Join(", ", Values.Select(v => $"'{v}'"));

    public string Kind => string.Join(" | ", Values);

    // The aliases are in the schema's enum too, so a stored slide that is read and sent back still passes a
    // client that checks the schema. The default goes in the field's description, not a "default" keyword.
    public JsonObject ToJsonSchema() => new()
    {
        ["type"] = "string",
        ["enum"] = new JsonArray(Values.Concat(Aliases.Keys).Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()),
    };

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        var text = value.GetString();
        if (!Values.Contains(text, StringComparer.Ordinal) && !Aliases.ContainsKey(text!))
        {
            validation.Fail(path, $"expected {Expected} (got '{EchoText.Of(text!)}')");
        }
    }
}
