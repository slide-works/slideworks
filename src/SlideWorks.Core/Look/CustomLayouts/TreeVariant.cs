namespace SlideWorks.Core.Look.CustomLayouts;

// An enum slot an element takes one class from (data-variant). As the markup stage returns it, it is
// the bare slot name and Classes is null. In a compiled tree the pipeline has written Classes: one
// finished class for each value of the enum, in the enum's order. The client adds the class of the
// slide's value and builds none itself.
public sealed record TreeVariant(string Slot, IReadOnlyList<VariantClass>? Classes)
{
    // The author's class behind a value's class, before the layout's prefix.
    public const string CLASS_START = "is-";

    // The variant as the markup stage writes it.
    public static TreeVariant Bare(string slot) => new(slot, null);

    // The class of one value of the enum, or null for a value the map does not hold.
    public string? ClassOf(string value) =>
        Classes?.FirstOrDefault(entry => string.Equals(entry.Value, value, StringComparison.Ordinal))?.Class;
}
