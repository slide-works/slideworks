namespace SlideWorks.Core.Look.CustomLayouts;

// The checked node tree of a custom layout's markup: what the client draws instead of HTML. As the
// markup stage returns it, it has no scope and the author's bare class names. The pipeline, and only
// the pipeline, sets Scope (the 8 lower-case hex characters of the class prefix) and writes every
// class with the prefix and gives every variant the finished class of each value of its enum
// (CompiledTree): a tree with a scope is a compiled tree, and its class names are final.
public sealed record LayoutTree(int Version, string? Scope, IReadOnlyList<TreeNode> Root)
{
    public const int VERSION = 1;

    // Every node, parents before their children.
    public IEnumerable<TreeNode> Nodes() => Root.SelectMany(node => node.WithDescendants());

    // The names of the CSS variables the markup binds to slots, each once, in document order.
    public IReadOnlyList<string> VarNames() =>
        Nodes().SelectMany(node => node.Vars).Select(bound => bound.Name).Distinct(StringComparer.Ordinal).ToList();
}
