using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// The check of each kind of template file. The last check registered for a kind is the one that runs, as the last
// registration of any service is the one resolved; a kind with no check stores nothing.
[RegisterSingleton]
public sealed class AssetChecks(IEnumerable<IAssetCheck> checks)
{
    private readonly IReadOnlyList<IAssetCheck> _checks = [.. checks];

    public IAssetCheck For(TemplateAssetKind kind)
        => _checks.LastOrDefault(check => check.Kind == kind) ?? throw new InvalidOperationException($"No check is registered for a template's {kind} files.");
}
