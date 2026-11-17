namespace SlideWorks.Core.Look.CustomLayouts;

// The checked node tree of a custom layout's markup: what the client draws instead of HTML. Scope is
// the 8 lower-case hex characters of the class prefix; only the pipeline sets it.
public sealed record LayoutTree(int Version, string? Scope, IReadOnlyList<TreeNode> Root)
{
    public const int VERSION = 1;

    // Every node, parents before their children.
    public IEnumerable<TreeNode> Nodes() => Root.SelectMany(node => node.WithDescendants());

    // The names of the CSS variables the markup binds to slots, each once, in document order.
    public IReadOnlyList<string> VarNames() =>
        Nodes().SelectMany(node => node.Vars).Select(bound => bound.Name).Distinct(StringComparer.Ordinal).ToList();
}
