namespace SlideWorks.Core.Look.CustomLayouts;

// Stage 4, run only when the first three passed: checks the tree against the schema and says how the
// layout reveals. Steps is null exactly when there are errors.
public interface ILayoutCrossCheck
{
    (CustomSteps? Steps, IReadOnlyList<CustomLayoutError> Errors) Check(LayoutTree tree, CustomLayoutSchema schema);
}
