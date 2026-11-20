using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// The body of POST /api/templates/{id}/assets. RightsConfirmed is the person's word that they may serve a font; it is
// kept only for a font, and never from an agent.
public sealed record StartAssetUploadRequest(
    [property: JsonRequired] TemplateAssetKind Kind,
    string? FileName,
    [property: JsonRequired] bool RightsConfirmed);
