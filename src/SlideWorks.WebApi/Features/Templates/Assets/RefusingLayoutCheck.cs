using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// TODO: refuses every custom layout until the real check lands, which parses the layout's markup, CSS and slots and
// stores the checked tree.
[RegisterSingleton(For = typeof(IAssetCheck), OfCollection = true)]
public sealed class RefusingLayoutCheck : IAssetCheck
{
    public TemplateAssetKind Kind => TemplateAssetKind.Layout;

    public Task<AssetCheckResult> CheckAsync(Stream content, string fileName, CancellationToken ct)
        => Task.FromResult(AssetCheckResult.Refused(TemplateErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE));
}
