using System.Text.Json;

namespace SlideWorks.WebApi.Features.Templates.Views;

// GET and PUT /api/templates/{id}: the template editor (src/contracts/screens/template-editor.json). Manifest is the
// draft's manifest (T3); each of its file fields is an asset id that Assets also lists. Version is 0 before the first
// publish.
public sealed record TemplateEditorView(
    string Id,
    string Name,
    int Revision,
    TemplateStatus Status,
    int Version,
    JsonElement Manifest,
    IReadOnlyList<TemplateAssetView> Assets,
    IReadOnlyList<TemplateChecklistRow> Checklist,
    IReadOnlyList<TemplateVersionView> Versions,
    bool CanPublish);
