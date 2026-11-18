namespace SlideWorks.Core.Look.CustomLayouts;

// A stored compiled layout that is well-formed JSON but can't be trusted: a class without the
// layout's own prefix, a variant map that is not its enum's, a name that is not its schema's. The
// message names the layout (the checked name of its schema, or UNNAMED when that name is itself not a
// layout name) and the rule, never a piece of what was stored. A caller that reads many layouts
// catches this for each one and leaves that layout out.
public sealed class StoredLayoutRefusedException(string layoutName, string rule)
    : Exception($"The stored layout '{layoutName}' was refused: {rule}.")
{
    // What the message calls a layout whose stored name can't be shown.
    public const string UNNAMED = "unnamed";

    public string LayoutName { get; } = layoutName;
}
