using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// TODO: accepts every file without reading it until the real check lands, which checks that a font is a woff2 file whose average glyph width is inside the allowed range, and stores that width as {"avgCharEm": …}.
[RegisterSingleton(For = typeof(IAssetCheck), OfCollection = true)]
public sealed class PassThroughFontCheck : IAssetCheck
{
    public TemplateAssetKind Kind => TemplateAssetKind.Font;

    public Task<AssetCheckResult> CheckAsync(Stream content, string fileName, CancellationToken ct)
        => Task.FromResult(AssetCheckResult.Accepted(AssetContentTypes.WOFF2));
}
