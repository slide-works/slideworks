using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

public sealed class ObjectSlot(IReadOnlyList<SlotField> fields, IReadOnlyList<string>? exactlyOneOf = null) : ISlotType
{
    public IReadOnlyList<SlotField> Fields { get; } = fields;

    // When set, exactly one of these fields must be present (two-column's left and right).
    public IReadOnlyList<string> ExactlyOneOf { get; } = exactlyOneOf ?? [];

    public string Expected => "an object";

    public string Kind => "object";

    public JsonObject ToJsonSchema()
    {
        var properties = new JsonObject();
        foreach (var field in Fields)
        {
            var schema = field.Type.ToJsonSchema();
            if (field.Description is not null)
            {
                schema["description"] = field.Description;
            }

            properties[field.Name] = schema;
        }

        var result = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new JsonArray(Fields.Where(f => f.Required).Select(f => (JsonNode?)JsonValue.Create(f.Name)).ToArray()),
            ["additionalProperties"] = false,
        };
        if (ExactlyOneOf.Count > 0)
        {
            result["oneOf"] = new JsonArray(ExactlyOneOf
                .Select(name => (JsonNode?)new JsonObject { ["required"] = new JsonArray(JsonValue.Create(name)) })
                .ToArray());
        }

        return result;
    }

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        // A repeated name would be read last-wins here and first-wins (or throw) elsewhere, so refuse it.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in value.EnumerateObject().Select(property => property.Name))
        {
            if (!seen.Add(name))
            {
                validation.Fail($"{path}.{name}", "duplicate field");
            }
            else if (!Fields.Any(f => string.Equals(f.Name, name, StringComparison.Ordinal)))
            {
                validation.Fail($"{path}.{name}", "unknown field");
            }
        }

        foreach (var field in Fields)
        {
            var fieldPath = $"{path}.{field.Name}";
            if (IsPresent(value, field.Name, out var fieldValue))
            {
                field.Type.Validate(fieldValue, fieldPath, validation);
            }
            else if (field.Required)
            {
                validation.Fail(fieldPath, "required");
            }
        }

        var present = ExactlyOneOf.Count(name => IsPresent(value, name, out _));
        if (ExactlyOneOf.Count > 0 && present != 1)
        {
            validation.Fail(path, $"exactly one of {string.Join(", ", ExactlyOneOf)}");
        }
    }

    // A JSON null counts as absent, so an agent may send null for an optional slot.
    public static bool IsPresent(JsonElement value, string name, out JsonElement fieldValue) =>
        value.TryGetProperty(name, out fieldValue) && fieldValue.ValueKind != JsonValueKind.Null;
}
