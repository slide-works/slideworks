using SlideWorks.Core.Look.CustomLayouts.CrossCheck;

namespace SlideWorks.Core.Look.CustomLayouts;

// Stage 4 (ILayoutCrossCheck): names and kinds against the schema, every slot used, the steps rule and
// the drawn-node limit. Returns every error, up to MAX_ERRORS; never throws for author input.
public sealed class LayoutCrossCheck : ILayoutCrossCheck
{
    public const int MAX_ERRORS = 20;

    public (CustomSteps? Steps, IReadOnlyList<CustomLayoutError> Errors) Check(LayoutTree tree, CustomLayoutSchema schema)
    {
        var steps = StepsRule.Of(tree, schema);
        List<CustomLayoutError> errors = [.. BindingWalk.Run(tree, schema)];
        if (steps is null)
        {
            errors.Add(CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.STEPS_MISMATCH));
        }

        IReadOnlyList<CustomLayoutError> found = [.. errors.Distinct().Take(MAX_ERRORS)];
        return found.Count > 0 ? (null, found) : (steps, found);
    }
}
