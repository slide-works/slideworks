namespace SlideWorks.Core.Look.CustomLayouts;

public sealed class LayoutCrossCheck : ILayoutCrossCheck
{
    // TODO: refuses every layout until the cross-check is written; the real body checks that every
    // binding names a slot of the right kind, that every slot is bound, the steps rule and the
    // drawn-node limit.
    public (CustomSteps? Steps, IReadOnlyList<CustomLayoutError> Errors) Check(LayoutTree tree, CustomLayoutSchema schema) =>
        (null, [CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE)]);
}
