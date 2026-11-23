using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// Pulls the repeated icon, emphasis and callout schemas out of a list_layouts response into one $defs object, and
// points each use at it with $ref. The ref is against the list_layouts response, which is where $defs lives, not
// against a layout schema copied out on its own. A description stays on the field.
public static class SharedLayoutSchemas
{
    public static JsonObject Share(IEnumerable<JsonObject> schemas)
    {
        var hits = new Dictionary<string, Hit>(StringComparer.Ordinal);
        foreach (var schema in schemas)
        {
            Collect(schema, hits);
        }

        var shared = hits.Values.Where(hit => hit.Uses > 1).ToList();
        if (shared.Count == 0)
        {
            return new JsonObject();
        }

        var defs = new JsonObject();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var hit in shared.OrderBy(hit => hit.Group, StringComparer.Ordinal).ThenBy(hit => hit.BodyKey, StringComparer.Ordinal))
        {
            var seen = used.GetValueOrDefault(hit.Group);
            used[hit.Group] = seen + 1;
            var name = seen == 0 ? hit.Group : $"{hit.Group}-{seen + 1}";
            names[hit.BodyKey] = name;
            defs[name] = hit.Schema.DeepClone();
        }

        foreach (var schema in schemas)
        {
            Replace(schema, names);
        }

        return defs;
    }

    private static void Collect(JsonNode? node, Dictionary<string, Hit> hits)
    {
        if (node is not JsonObject obj)
        {
            return;
        }

        if (obj["properties"] is JsonObject properties)
        {
            foreach (var property in properties)
            {
                if (property.Value is not JsonObject child)
                {
                    continue;
                }

                var hint = Hint(property.Key, child);
                if (hint is not null)
                {
                    var body = WithoutDescription(child);
                    var key = hint + "\n" + body.ToJsonString();
                    hits[key] = hits.TryGetValue(key, out var hit)
                        ? hit with { Uses = hit.Uses + 1 }
                        : new Hit(key, hint, 1, body);
                }

                Collect(child, hits);
            }
        }

        Collect(obj["items"], hits);
        if (obj["oneOf"] is JsonArray oneOf)
        {
            foreach (var item in oneOf)
            {
                Collect(item, hits);
            }
        }
    }

    private static void Replace(JsonNode? node, Dictionary<string, string> names)
    {
        if (node is not JsonObject obj)
        {
            return;
        }

        if (obj["properties"] is JsonObject properties)
        {
            foreach (var property in properties.ToList())
            {
                if (property.Value is not JsonObject child)
                {
                    continue;
                }

                var hint = Hint(property.Key, child);
                if (hint is not null)
                {
                    var key = hint + "\n" + WithoutDescription(child).ToJsonString();
                    if (names.TryGetValue(key, out var name))
                    {
                        var replacement = new JsonObject
                        {
                            ["allOf"] = new JsonArray(new JsonObject { ["$ref"] = $"#/$defs/{name}" }),
                        };
                        if (child["description"] is JsonNode description)
                        {
                            replacement["description"] = description.DeepClone();
                        }

                        properties[property.Key] = replacement;
                        continue;
                    }
                }

                Replace(child, names);
            }
        }

        Replace(obj["items"], names);
        if (obj["oneOf"] is JsonArray oneOf)
        {
            foreach (var item in oneOf)
            {
                Replace(item, names);
            }
        }
    }

    // icon is any string schema whose enum is the icon catalogue. emphasis and callout are shared by their field name.
    private static string? Hint(string name, JsonObject schema)
    {
        if (name is "emphasis" or "callout")
        {
            return name;
        }

        return IsStringType(schema["type"]) && schema["enum"] is JsonArray { Count: >= 20 } ? "icon" : null;
    }

    // An optional field's type is ["string", "null"].
    private static bool IsStringType(JsonNode? type) => type switch
    {
        JsonValue value => value.GetValue<string>() == "string",
        JsonArray array => array.Any(item => item?.GetValue<string>() == "string"),
        _ => false,
    };

    private static JsonObject WithoutDescription(JsonObject schema)
    {
        var copy = (JsonObject)schema.DeepClone();
        copy.Remove("description");
        return copy;
    }

    private sealed record Hit(string BodyKey, string Group, int Uses, JsonObject Schema);
}
