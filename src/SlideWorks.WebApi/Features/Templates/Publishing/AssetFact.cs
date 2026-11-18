using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// What a check needs to know of one stored file of the template: never its bytes.
public sealed record AssetFact(string Id, TemplateAssetKind Kind, bool RightsConfirmed, string? MetaJson);
