using System.Text.Json;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Views;

// One stored file of a template draft. Meta is what the upload check stored for the file, an object whose fields vary
// by kind: {"avgCharEm": 0.56} for a font, {"width": 1920, "height": 1080} for a PNG or WebP, and {} when there is
// nothing, as for an SVG. Url serves the file.
public sealed record TemplateAssetView(
    string AssetId,
    TemplateAssetKind Kind,
    string FileName,
    string ContentType,
    int Bytes,
    JsonElement Meta,
    bool RightsConfirmed,
    string Url);
