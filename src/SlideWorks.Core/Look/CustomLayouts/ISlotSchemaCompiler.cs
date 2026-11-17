using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Look.CustomLayouts;

// Stage 2: checks an uploaded slots.json and builds the definition slides are validated with.
// fixedSteps is the tree's highest numbered step (TreeSteps.FixedCount), 0 when it has none.
// Definition and Schema are null exactly when there are errors.
public interface ISlotSchemaCompiler
{
    (LayoutDefinition? Definition, CustomLayoutSchema? Schema, IReadOnlyList<CustomLayoutError> Errors) Compile(string slotsJson, int fixedSteps);
}
