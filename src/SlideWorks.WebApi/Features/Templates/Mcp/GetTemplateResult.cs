using System.Text.Json;
using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// get_template: the draft as the editor shows it. Version is the latest published version, null while only a draft
// exists. Decks counts the decks that use the template.
public sealed record GetTemplateResult(
    string Template,
    string Name,
    string Status,
    int Revision,
    int? Version,
    JsonElement Manifest,
    IReadOnlyList<TemplateFileInfo> Files,
    IReadOnlyList<TemplateCheck> Checks,
    DeckSharing Sharing,
    int Decks,
    string Url);
