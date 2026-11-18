using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// TODO: accepts every file without reading it until the real check lands, which checks that a piece of art is an SVG without scripts or outside references, a PNG or a WebP, and answers the file's own type and size.
// Until then the file is served as bytes of no type, so a browser neither draws nor runs it.
[RegisterSingleton(For = typeof(IAssetCheck), OfCollection = true)]
public sealed class PassThroughArtCheck : IAssetCheck
{
    public TemplateAssetKind Kind => TemplateAssetKind.Art;

    public Task<AssetCheckResult> CheckAsync(Stream content, string fileName, CancellationToken ct)
        => Task.FromResult(AssetCheckResult.Accepted(AssetContentTypes.UNKNOWN));
}
