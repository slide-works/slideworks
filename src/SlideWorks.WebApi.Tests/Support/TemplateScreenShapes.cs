using System.Text.Json;
using SlideWorks.WebApi.Features.Templates.Views;

namespace SlideWorks.WebApi.Tests.Support;

// The nullable and opaque paths of the template screens in src/contracts/screens, for JsonShape.AssertMatches, and the
// samples themselves as view records (what the template fakes answer). JsonShape checks names and value kinds only.
public static class TemplateScreenShapes
{
    public const string TEMPLATES_FILE = "templates.json";
    public const string EDITOR_FILE = "template-editor.json";
    public const string SHARING_FILE = "template-sharing.json";
    public const string PUBLIC_FILE = "template-public.json";
    public const string LAYOUT_REVIEW_FILE = "layout-review.json";

    // The owner is null on the caller's own templates and on the built-in ones.
    public static readonly JsonShapeOptions Templates = new() { NullablePaths = ["mine[].owner", "builtIn[].owner"] };

    // The manifest is T3's (template-manifest.schema.json), and an asset's meta varies by kind.
    public static readonly JsonShapeOptions Editor = new() { OpaquePaths = ["manifest", "assets[].meta"] };

    public static readonly JsonShapeOptions Checklist = new();

    public static readonly JsonShapeOptions Sharing = new();

    public static readonly JsonShapeOptions Public = new() { NullablePaths = ["look.template.titleArt", "look.logo"] };

    // The compiled tree, the slots and the two sample slides vary by layout.
    public static readonly JsonShapeOptions LayoutReview = new() { OpaquePaths = ["layout.tree", "layout.slots", "sample", "maximum"] };

    // A screen sample as its view record, read the way the wire is written.
    public static T Sample<T>(string file) => TemplateViewJson.Read<T>(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), file)));

    // The checklist route's shape: the checklist and canPublish of the editor sample.
    public static JsonElement ChecklistExample()
    {
        using var editor = JsonDocument.Parse(File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), EDITOR_FILE)));
        return JsonSerializer.SerializeToElement(new Dictionary<string, JsonElement>
        {
            ["checklist"] = editor.RootElement.GetProperty("checklist").Clone(),
            ["canPublish"] = editor.RootElement.GetProperty("canPublish").Clone(),
        });
    }
}
