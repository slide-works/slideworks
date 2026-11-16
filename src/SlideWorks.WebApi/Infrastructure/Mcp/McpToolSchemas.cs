using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace SlideWorks.WebApi.Infrastructure.Mcp;

// Tightens the inputSchema the SDK generates for each tool, once at startup (McpSetup), so what an agent's model reads is
// exactly what McpArgumentCheck enforces, since the check reads this same schema:
// - Every object schema that lists its properties gets additionalProperties: false: the arguments themselves and entries
//   such as set_notes' notes items. A misspelt argument ("viewer", "positon") is then refused, not silently dropped.
// - An argument the SDK leaves untyped (a JsonElement parameter, which is only ever the slots) is advertised as an
//   object. Its fields stay open here: SlideValidator checks them against the layout.
// - A list with a format (where [EmailAddress] on a list parameter puts it) moves the format onto its entries, where JSON
//   Schema applies it, and its entries are no longer nullable: the generator marks list entries nullable whatever the
//   C# annotation says.
public static class McpToolSchemas
{
    public static void Tighten(Tool tool)
    {
        if (JsonNode.Parse(tool.InputSchema.GetRawText()) is not JsonObject schema)
        {
            return;
        }
        if (schema["properties"] is JsonObject arguments)
        {
            foreach (var (_, argument) in arguments)
            {
                if (argument is JsonObject untyped && !untyped.ContainsKey("type"))
                {
                    untyped["type"] = "object";
                }
            }
        }
        Close(schema);
        tool.InputSchema = JsonSerializer.SerializeToElement(schema);
    }

    private static void Close(JsonObject schema)
    {
        if (schema["properties"] is JsonObject properties)
        {
            schema["additionalProperties"] = false;
            foreach (var (_, property) in properties)
            {
                if (property is JsonObject child)
                {
                    Close(child);
                }
            }
        }
        if (schema["items"] is JsonObject items)
        {
            if (schema["format"] is { } format)
            {
                schema.Remove("format");
                items["format"] = format;
                items["type"] = NotNull(items["type"]);
            }
            Close(items);
        }
    }

    private static JsonNode? NotNull(JsonNode? type)
    {
        if (type is not JsonArray types)
        {
            return type?.DeepClone();
        }
        var kept = types.Select(t => t?.GetValue<string>()).Where(t => t is not null and not "null").ToList();
        return kept.Count == 1 ? JsonValue.Create(kept[0]) : new JsonArray(kept.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray());
    }
}
