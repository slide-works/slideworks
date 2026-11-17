namespace SlideWorks.Core.Look.CustomLayouts;

public sealed class MarkupSanitiser : IMarkupSanitiser
{
    // TODO: refuses every upload until the markup sanitiser is written; the real body parses the
    // markup against the allow-list and returns the tree.
    public (LayoutTree? Tree, IReadOnlyList<CustomLayoutError> Errors) Sanitise(string markup) =>
        (null, [CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE)]);
}
