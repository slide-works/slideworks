using System.Text.Json;
using SlideWorks.Core;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Views;

namespace SlideWorks.WebApi.Features.Templates.Drafts;

// The pieces of the editor view (template-editor.json) that are made from rows.
public static class TemplateEditorViews
{
    public static TemplateAssetView Asset(TemplateAsset asset) => new(
        asset.Id,
        asset.Kind,
        asset.FileName,
        asset.ContentType,
        (int)asset.Bytes,
        Json(asset.MetaJson ?? "{}"),
        asset.RightsConfirmed,
        $"/api/templates/{asset.TemplateId}/assets/{asset.Id}");

    public static TemplateVersionView Published(TemplateVersion version)
        => new(version.Version, DisplayDate.Day(version.PublishedAt ?? throw new InvalidOperationException("A published version has a date.")));

    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
