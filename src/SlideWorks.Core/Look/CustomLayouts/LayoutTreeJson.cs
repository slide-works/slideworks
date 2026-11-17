using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Look.CustomLayouts;

// The tree's JSON (T10): { version, scope?, root: [ { tag, classes, slot?, each?, if?, step?, vars,
// variants, children } ] }. Lists are always present, an unset optional field is left out, and a key
// that is not in the contract is refused: the client draws exactly what is here.
//
// variants has two forms and a tree holds one of them throughout. The author's side (no scope) lists
// bare slot names: ["state"]. The compiled side (with a scope) lists the server-written classes:
// [ { "slot": "state", "classes": { "normal": "cl-…__is-normal", "alert": "cl-…__is-alert" } } ].
//
// Read is bounded by CustomLayoutLimits (depth, nodes, the highest step), so a stored tree is never
// larger than one that could have been compiled. Whatever is wrong, it throws JsonException, and no
// message quotes a piece of the document.
public static class LayoutTreeJson
{
    private const string WHAT = "A layout tree";

    private static readonly string[] TreeKeys = ["version", "scope", "root"];

    private static readonly string[] NodeKeys = ["tag", "classes", "slot", "each", "if", "step", "vars", "variants", "children"];

    private static readonly string[] VarKeys = ["name", "slot"];

    private static readonly string[] VariantKeys = ["slot", "classes"];

    public static string Write(LayoutTree tree) => ToNode(tree).ToJsonString();

    public static LayoutTree Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Read(document.RootElement);
    }

    public static LayoutTree Read(JsonElement json)
    {
        StrictJson.OnlyKeys(json, TreeKeys, WHAT);
        var version = StrictJson.Whole(StrictJson.Member(json, "version", WHAT), WHAT);
        if (version != LayoutTree.VERSION)
        {
            throw new JsonException("A layout tree of this version can't be read.");
        }

        var scope = Text(json, "scope");
        if (scope is not null && !LayoutScope.IsValid(scope))
        {
            throw new JsonException("A layout tree's scope is 8 lower-case hex characters.");
        }

        var reading = new Reading(scope is not null);
        return new LayoutTree(version, scope, List(json, "root", node => reading.Node(node, 1)));
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
        json["variants"] = new JsonArray([.. node.Variants.Select(ToNode)]);
        json["children"] = new JsonArray([.. node.Children.Select(ToNode)]);
        return json;
    }

    private static JsonNode ToNode(TreeVariant variant)
    {
        if (variant.Classes is null)
        {
            return variant.Slot;
        }

        var classes = new JsonObject();
        foreach (var entry in variant.Classes)
        {
            classes[entry.Value] = entry.Class;
        }

        return new JsonObject { ["slot"] = variant.Slot, ["classes"] = classes };
    }

    private static void Set(JsonObject json, string key, string? value)
    {
        if (value is not null)
        {
            json[key] = value;
        }
    }

    private static List<T> List<T>(JsonElement json, string key, Func<JsonElement, T> read) =>
        json.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Select(read)]
            : throw new JsonException($"A layout tree needs the list '{key}'.");

    private static string Name(JsonElement json) => StrictJson.Text(json, WHAT);

    private static string? Text(JsonElement json, string key) =>
        json.TryGetProperty(key, out var value) ? Name(value) : null;

    // One read of one tree: whether it is the compiled side, and how many nodes it has met so far.
    private sealed class Reading(bool compiled)
    {
        private int _nodes;

        public TreeNode Node(JsonElement json, int depth)
        {
            if (depth > CustomLayoutLimits.MAX_MARKUP_DEPTH || ++_nodes > CustomLayoutLimits.MAX_NODES)
            {
                throw new JsonException("A layout tree is deeper or larger than a layout may be.");
            }

            StrictJson.OnlyKeys(json, NodeKeys, WHAT);
            int? step = json.TryGetProperty("step", out var value) ? StrictJson.Whole(value, WHAT) : null;
            if (step is < TreeNode.STEP_EACH or > CustomLayoutLimits.MAX_STEPS)
            {
                throw new JsonException("A step is 0, or a number up to the highest step a layout may have.");
            }

            return new TreeNode(
                Required(json, "tag"),
                List(json, "classes", Name),
                Text(json, "slot"),
                Text(json, "each"),
                Text(json, "if"),
                step,
                List(json, "vars", ReadVar),
                List(json, "variants", ReadVariant),
                List(json, "children", child => Node(child, depth + 1)));
        }

        private static TreeVar ReadVar(JsonElement json)
        {
            StrictJson.OnlyKeys(json, VarKeys, WHAT);
            return new TreeVar(Required(json, "name"), Required(json, "slot"));
        }

        private TreeVariant ReadVariant(JsonElement json)
        {
            if (!compiled)
            {
                return TreeVariant.Bare(Name(json));
            }

            StrictJson.OnlyKeys(json, VariantKeys, WHAT);
            var classes = StrictJson.Member(json, "classes", WHAT);
            if (classes.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("A compiled variant's classes are an object of value and class.");
            }

            return new TreeVariant(Required(json, "slot"), [.. classes.EnumerateObject().Select(entry => new VariantClass(entry.Name, Name(entry.Value)))]);
        }

        private static string Required(JsonElement json, string key) =>
            Text(json, key) ?? throw new JsonException($"A layout tree needs '{key}'.");
    }
}
