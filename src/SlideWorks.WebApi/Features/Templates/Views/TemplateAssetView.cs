using System.Text.Json;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Views;

// One stored file of a template draft. Meta is what the upload check found (a font's family, weight and width; an
// image's size), as stored. Url serves the file.
public sealed record TemplateAssetView(
    string AssetId,
    TemplateAssetKind Kind,
    string FileName,
    string ContentType,
    int Bytes,
    JsonElement Meta,
    bool RightsConfirmed,
    string Url);
