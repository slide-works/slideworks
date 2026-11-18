using SlideWorks.Core.Look.Manifest;

namespace SlideWorks.WebApi.Features.Templates.Publishing;

// The manifest about to be published and the files of its template, as the checks read them.
public sealed record PublishFacts(TemplateManifest Manifest, IReadOnlyList<AssetFact> Assets)
{
    public AssetFact? Asset(string id) => Assets.FirstOrDefault(asset => string.Equals(asset.Id, id, StringComparison.Ordinal));
}
