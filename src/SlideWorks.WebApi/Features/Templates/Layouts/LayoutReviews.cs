using System.Text.Json;
using System.Text.Json.Nodes;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Features.Templates.Views;

namespace SlideWorks.WebApi.Features.Templates.Layouts;

// The review view of a stored layout (src/contracts/screens/layout-review.json). SafeArea and Contrast hold the report
// the owner's page sent with the approval: before one, nothing is measured and both are not ok. An approval is only
// ever written from a report with no findings, so an approved layout's lists are empty and both are ok.
public static class LayoutReviews
{
    private const string REVEAL = "reveal";
    private const string ONE_BY_ONE = "one-by-one";

    public static LayoutReviewView ViewOf(TemplateLayout layout)
    {
        var compiled = layout.Layout;
        var sample = Json(compiled.SampleJson);
        var slots = Json(compiled.SlotsJson).GetProperty("slots").Clone();
        var tree = Json(LayoutTreeJson.Write(compiled.Tree));
        return new LayoutReviewView(
            compiled.Name,
            layout.AssetId,
            layout.Approved,
            compiled.Surface,
            SlideSteps.Count(compiled.Definition, WithReveal(compiled, sample)),
            new LayoutReviewLayout(compiled.Name, compiled.Surface, tree, compiled.Css, slots),
            sample,
            Json(compiled.MaximumJson),
            new LayoutReviewSafeArea(layout.Approved, layout.Overflow),
            new LayoutReviewContrast(layout.Approved, layout.Contrast));
    }

    // The sample as the review page plays it: a layout with steps reveals one by one.
    private static JsonElement WithReveal(CompiledLayout layout, JsonElement sample)
    {
        if (layout.Steps.Kind == CustomSteps.NONE || sample.ValueKind != JsonValueKind.Object)
        {
            return sample;
        }

        var node = JsonNode.Parse(sample.GetRawText())!.AsObject();
        node[REVEAL] = ONE_BY_ONE;
        return Json(node.ToJsonString());
    }

    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
