using System.Text.Json;

namespace SlideWorks.Core.Slides;

// How many steps a slide has (K2, K16): what the layout's step plan counts, plus one for a callout
// that reveals last, with or without a plan. Expects slots that passed SlideValidator.
public static class SlideSteps
{
    public static int Count(string layout, JsonElement slots) =>
        LayoutCatalogue.Find(layout) is { } definition ? Count(definition, slots) : 0;

    public static int Count(LayoutDefinition definition, JsonElement slots) =>
        definition.Steps?.Count(slots) ?? StepPlan.CalloutStep(slots);
}
