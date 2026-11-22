using SlideWorks.Core.Look.CustomLayouts.Schema;
using SlideWorks.Core.Slides;
using static SlideWorks.Core.Slides.SlotKit;

namespace SlideWorks.Core.Look.CustomLayouts;

// Stage 2 (T10): checks an uploaded slots.json and builds the definition slides are validated with. It
// never throws for what an author wrote: a problem is an error with a path, at most 20 of them.
public sealed class SlotSchemaCompiler : ISlotSchemaCompiler
{
    public (LayoutDefinition? Definition, CustomLayoutSchema? Schema, IReadOnlyList<CustomLayoutError> Errors) Compile(string slotsJson, int fixedSteps)
    {
        var reader = new SlotSchemaReader();
        if (!reader.Read(slotsJson) || reader.Schema is not { } schema)
        {
            return (null, null, reader.Errors);
        }

        var plan = PlanOf(schema, fixedSteps);

        // The reveal slot is ours: a slot of that name is refused in the file.
        IReadOnlyList<SlotField> fields = plan is null ? reader.Fields : [.. reader.Fields, Opt("reveal", Reveal(), REVEAL)];

        return (new LayoutDefinition(schema.Name, schema.Description, reader.Surface, fields, plan), schema, []);
    }

    private static StepPlan? PlanOf(CustomLayoutSchema schema, int fixedSteps)
    {
        if (schema.Steps is { } list)
        {
            return StepPlan.List(list);
        }

        return fixedSteps > 0 ? StepPlan.Fixed(fixedSteps, "the layout's steps") : null;
    }
}
