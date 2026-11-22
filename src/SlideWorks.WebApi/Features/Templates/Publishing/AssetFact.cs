using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// What a check needs to know of one stored file of the template: never its bytes. Layout is set for a custom layout
// that could be read, and is null for any other file and for a layout that could not.
public sealed record AssetFact(string Id, TemplateAssetKind Kind, bool RightsConfirmed, string? MetaJson, LayoutFact? Layout = null);

// The name a stored layout was compiled under, and whether its owner approved it.
public sealed record LayoutFact(string Name, bool Approved);
