using System.Text;
using System.Text.Json;

namespace SlideWorks.Core.Look.CustomLayouts;

// The stored form of a compiled layout (T10): { compilerVersion, name, description, surface,
// steps: { kind, list?, count }, tree, css, schema, sample, maximum }. schema is the whole uploaded
// slots.json; the definition is not stored but built again from it on read.
public static class CompiledLayoutJson
{
    public static string Write(CompiledLayout layout)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("compilerVersion", layout.CompilerVersion);
            writer.WriteString("name", layout.Name);
            writer.WriteString("description", layout.Description);
            writer.WriteString("surface", layout.Surface);
            writer.WriteStartObject("steps");
            writer.WriteString("kind", layout.Steps.Kind);
            if (layout.Steps.List is not null)
            {
                writer.WriteString("list", layout.Steps.List);
            }

            writer.WriteNumber("count", layout.Steps.Count);
            writer.WriteEndObject();
            writer.WritePropertyName("tree");
            LayoutTreeJson.ToNode(layout.Tree).WriteTo(writer);
            writer.WriteString("css", layout.Css);
            writer.WritePropertyName("schema");
            writer.WriteRawValue(layout.SlotsJson);
            writer.WritePropertyName("sample");
            writer.WriteRawValue(layout.SampleJson);
            writer.WritePropertyName("maximum");
            writer.WriteRawValue(layout.MaximumJson);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    // A row written before compilerVersion existed reads as version 0. The compiler builds the
    // definition from the stored schema; a schema it now refuses throws, because nothing filters
    // stored layouts by version yet.
    public static CompiledLayout Read(string json, ISlotSchemaCompiler compiler)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tree = LayoutTreeJson.Read(root.GetProperty("tree"));
        var slotsJson = root.GetProperty("schema").GetRawText();
        var (definition, _, errors) = compiler.Compile(slotsJson, TreeSteps.FixedCount(tree));
        if (definition is null || errors.Count > 0)
        {
            throw new InvalidOperationException("A stored layout's schema no longer compiles: " + string.Join(", ", errors.Select(error => error.Code)));
        }

        var steps = root.GetProperty("steps");
        return new CompiledLayout(
            root.TryGetProperty("compilerVersion", out var version) ? version.GetInt32() : 0,
            Text(root, "name"),
            Text(root, "description"),
            Text(root, "surface"),
            new CustomSteps(Text(steps, "kind"), steps.TryGetProperty("list", out var list) ? list.GetString() : null, steps.GetProperty("count").GetInt32()),
            definition,
            tree,
            Text(root, "css"),
            slotsJson,
            root.GetProperty("sample").GetRawText(),
            root.GetProperty("maximum").GetRawText());
    }

    private static string Text(JsonElement json, string key) =>
        json.GetProperty(key).GetString() ?? throw new JsonException($"A stored layout needs '{key}'.");
}
