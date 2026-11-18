using System.Text;
using System.Text.Json;

namespace SlideWorks.Core.Look.CustomLayouts;

// The stored form of a compiled layout (T10): { compilerVersion, name, description, surface,
// steps: { kind, list?, count }, tree, css, schema, sample, maximum }. schema is the whole uploaded
// slots.json; the definition is not stored but built again from it on read.
public static class CompiledLayoutJson
{
    private const string WHAT = "A stored layout";

    private static readonly string[] Keys = ["compilerVersion", "name", "description", "surface", "steps", "tree", "css", "schema", "sample", "maximum"];

    private static readonly string[] StepsKeys = ["kind", "list", "count"];

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

    // Strict and fail closed: this is the last check before a layout is drawn in every viewer's
    // browser. A document that is not the contract's shape (an unknown or missing member, a steps
    // kind that does not exist, a tree over CustomLayoutLimits) throws JsonException. One that has
    // the shape but can't be trusted throws StoredLayoutRefusedException: a schema name that is not a
    // layout name (checked first, and the message then says "unnamed": no message carries a name
    // that was not checked), a name or surface that is not its schema's, a tree without a scope, a
    // class anywhere in the tree (a variant's classes included) that does not start with the layout's
    // own prefix, a variant map that is not one class for each value of its enum. A schema today's
    // compiler refuses throws InvalidOperationException. Nothing stored makes Read throw anything
    // else: a caller that reads several layouts catches the three for each one, so one bad row does
    // not take the others down. A row written before compilerVersion existed reads as version 0.
    public static CompiledLayout Read(string json, ISlotSchemaCompiler compiler)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        StrictJson.OnlyKeys(root, Keys, WHAT);
        var tree = LayoutTreeJson.Read(Member(root, "tree"));
        var slotsJson = Member(root, "schema").GetRawText();
        var (definition, schema, errors) = compiler.Compile(slotsJson, TreeSteps.FixedCount(tree));
        if (definition is null || schema is null || errors.Count > 0)
        {
            throw new InvalidOperationException("A stored layout's schema no longer compiles: " + string.Join(", ", errors.Select(error => error.Code)));
        }

        if (!LayoutClassPrefix.IsLayoutName(schema.Name))
        {
            throw new StoredLayoutRefusedException(StoredLayoutRefusedException.UNNAMED, "its name is not a layout name");
        }

        var name = Text(root, "name");
        var surface = Text(root, "surface");
        if (name != schema.Name || surface != schema.Surface)
        {
            throw new StoredLayoutRefusedException(schema.Name, "its name or surface is not its schema's");
        }

        if (tree.Scope is null)
        {
            throw new StoredLayoutRefusedException(schema.Name, "its tree has no scope, so it was never compiled");
        }

        CompiledTree.Verify(tree, schema, LayoutClassPrefix.Of(tree.Scope, schema.Name));

        return new CompiledLayout(
            root.TryGetProperty("compilerVersion", out var version) ? StrictJson.Whole(version, WHAT) : 0,
            name,
            Text(root, "description"),
            surface,
            ReadSteps(Member(root, "steps")),
            definition,
            tree,
            Text(root, "css"),
            slotsJson,
            Member(root, "sample").GetRawText(),
            Member(root, "maximum").GetRawText());
    }

    // Kind is one of the three; list is there exactly when the kind is list; count is 0 unless the
    // kind is fixed, and never over the highest step.
    private static CustomSteps ReadSteps(JsonElement json)
    {
        StrictJson.OnlyKeys(json, StepsKeys, WHAT);
        var kind = Text(json, "kind");
        var list = json.TryGetProperty("list", out var named) ? StrictJson.Text(named, WHAT) : null;
        var count = StrictJson.Whole(Member(json, "count"), WHAT);
        var known = kind is CustomSteps.NONE or CustomSteps.LIST or CustomSteps.FIXED;
        var counted = kind == CustomSteps.FIXED ? count is >= 1 and <= CustomLayoutLimits.MAX_STEPS : count == 0;
        if (!known || !counted || (kind == CustomSteps.LIST) != (list is not null))
        {
            throw new JsonException("A stored layout's steps are not none, a list or a fixed count.");
        }

        return new CustomSteps(kind, list, count);
    }

    private static JsonElement Member(JsonElement json, string key) => StrictJson.Member(json, key, WHAT);

    private static string Text(JsonElement json, string key) => StrictJson.Text(Member(json, key), WHAT);
}
