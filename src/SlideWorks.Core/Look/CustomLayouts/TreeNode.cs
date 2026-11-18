namespace SlideWorks.Core.Look.CustomLayouts;

// One element. Slot fills it with a slot's value, Each repeats it per item of a list, If draws it only
// when a slot is set, Step puts it on a later click (0 means one step per repeated item), Vars set CSS
// variables from number slots and Variants add one class per enum slot, by the slot's value. There is no text.
public sealed record TreeNode(
    string Tag,
    IReadOnlyList<string> Classes,
    string? Slot,
    string? Each,
    string? If,
    int? Step,
    IReadOnlyList<TreeVar> Vars,
    IReadOnlyList<TreeVariant> Variants,
    IReadOnlyList<TreeNode> Children)
{
    public const int STEP_EACH = 0;

    public IEnumerable<TreeNode> WithDescendants() => Children.SelectMany(child => child.WithDescendants()).Prepend(this);
}
