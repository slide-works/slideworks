using SlideWorks.Core.Slides;

namespace SlideWorks.Core.Look.CustomLayouts;

public sealed class SlotSchemaCompiler : ISlotSchemaCompiler
{
    // TODO: refuses every upload until the slot schema compiler is written; the real body checks
    // slots.json and builds the layout's definition and schema.
    public (LayoutDefinition? Definition, CustomLayoutSchema? Schema, IReadOnlyList<CustomLayoutError> Errors) Compile(string slotsJson, int fixedSteps) =>
        (null, null, [CustomLayoutError.Of(CustomLayoutError.LAYOUT_PATH, CustomLayoutErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE)]);
}
