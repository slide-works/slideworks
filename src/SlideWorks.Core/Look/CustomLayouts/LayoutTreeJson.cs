using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Look.CustomLayouts;

// The tree's JSON (T10): { version, scope?, root: [ { tag, classes, slot?, each?, if?, step?, vars,
// variants, children } ] }. Lists are always present, an unset optional field is left out, and a key
// that is not in the contract is refused: the client draws exactly what is here.
public static class LayoutTreeJson
{
    private static readonly string[] TreeKeys = ["version", "scope", "root"];

    private static readonly string[] NodeKeys = ["tag", "classes", "slot", "each", "if", "step", "vars", "variants", "children"];

    private static readonly string[] VarKeys = ["name", "slot"];

    public static string Write(LayoutTree tree) => ToNode(tree).ToJsonString();

    public static LayoutTree Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Read(document.RootElement);
    }

    public static LayoutTree Read(JsonElement json)
    {
        OnlyKeys(json, TreeKeys);
        var version = json.GetProperty("version").GetInt32();
        if (version != LayoutTree.VERSION)
        {
            throw new JsonException($"A layout tree of version {version} can't be read.");
        }

        var scope = Text(json, "scope");
        if (scope is not null && !LayoutScope.IsValid(scope))
        {
            throw new JsonException("A layout tree's scope is 8 lower-case hex characters.");
        }

        return new LayoutTree(version, scope, List(json, "root", ReadNode));
    }

    public static JsonObject ToNode(LayoutTree tree)
    {
        var json = new JsonObject { ["version"] = tree.Version };
        if (tree.Scope is not null)
        {
            json["scope"] = tree.Scope;
        }

        json["root"] = new JsonArray([.. tree.Root.Select(ToNode)]);
        return json;
    }

    private static JsonNode ToNode(TreeNode node)
    {
        var json = new JsonObject
        {
            ["tag"] = node.Tag,
            ["classes"] = new JsonArray([.. node.Classes.Select(name => (JsonNode)name)]),
        };
        Set(json, "slot", node.Slot);
        Set(json, "each", node.Each);
        Set(json, "if", node.If);
        if (node.Step is { } step)
        {
            json["step"] = step;
        }

        json["vars"] = new JsonArray([.. node.Vars.Select(bound => (JsonNode)new JsonObject { ["name"] = bound.Name, ["slot"] = bound.Slot })]);
        json["variants"] = new JsonArray([.. node.Variants.Select(slot => (JsonNode)slot)]);
        json["children"] = new JsonArray([.. node.Children.Select(ToNode)]);
        return json;
    }

    private static void Set(JsonObject json, string key, string? value)
    {
        if (value is not null)
        {
            json[key] = value;
        }
    }

    private static TreeNode ReadNode(JsonElement json)
    {
        OnlyKeys(json, NodeKeys);
        int? step = json.TryGetProperty("step", out var value) ? value.GetInt32() : null;
        if (step < 0)
        {
            throw new JsonException("A step is 0 or more.");
        }

        return new TreeNode(
            Required(json, "tag"),
            List(json, "classes", Name),
            Text(json, "slot"),
            Text(json, "each"),
            Text(json, "if"),
            step,
            List(json, "vars", ReadVar),
            List(json, "variants", Name),
            List(json, "children", ReadNode));
    }

    private static TreeVar ReadVar(JsonElement json)
    {
        OnlyKeys(json, VarKeys);
        return new TreeVar(Required(json, "name"), Required(json, "slot"));
    }

    private static void OnlyKeys(JsonElement json, string[] keys)
    {
        if (json.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A layout tree is made of objects.");
        }

        var unknown = json.EnumerateObject().Select(property => property.Name).FirstOrDefault(name => !keys.Contains(name, StringComparer.Ordinal));
        if (unknown is not null)
        {
            throw new JsonException($"A layout tree has no key '{unknown}'.");
        }
    }

    private static List<T> List<T>(JsonElement json, string key, Func<JsonElement, T> read) =>
        json.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Select(read)]
            : throw new JsonException($"A layout tree needs the list '{key}'.");

    private static string Required(JsonElement json, string key) =>
        Text(json, key) ?? throw new JsonException($"A layout tree needs '{key}'.");

    private static string Name(JsonElement json) =>
        json.ValueKind == JsonValueKind.String ? json.GetString()! : throw new JsonException("A layout tree's names are strings.");

    private static string? Text(JsonElement json, string key)
    {
        if (!json.TryGetProperty(key, out var value))
        {
            return null;
        }

        return Name(value);
    }
}
