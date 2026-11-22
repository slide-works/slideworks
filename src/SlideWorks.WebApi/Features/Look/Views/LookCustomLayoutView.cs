using System.Text.Json;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.WebApi.Features.Templates.Layouts;

namespace SlideWorks.WebApi.Features.Look.Views;

// One custom layout of the deck's template in a LookView (T7): its checked node tree, its css and its slots, as the
// client draws them. Tree is the checked tree as its JSON and Slots the `slots` object of the layout's slots.json; Css is
// the rewritten CSS. Nothing uploaded and unchecked is here.
public sealed record LookCustomLayoutView(string Name, string Surface, JsonElement Tree, string Css, JsonElement Slots)
{
    public static LookCustomLayoutView From(TemplateLayout stored)
    {
        var layout = stored.Layout;
        using var schema = JsonDocument.Parse(layout.SlotsJson);
        var slots = schema.RootElement.ValueKind == JsonValueKind.Object && schema.RootElement.TryGetProperty("slots", out var found)
            ? found.Clone()
            : JsonSerializer.SerializeToElement(new { });
        return new LookCustomLayoutView(layout.Name, layout.Surface, JsonSerializer.SerializeToElement(LayoutTreeJson.ToNode(layout.Tree)), layout.Css, slots);
    }
}
