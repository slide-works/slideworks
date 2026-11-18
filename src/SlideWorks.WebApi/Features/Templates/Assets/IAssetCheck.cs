using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// The check of one kind of template file (T6). TemplateAssets runs the kind's check on every file before it is stored,
// and stores nothing the check refuses. content is the whole file, already inside the kind's size limit, positioned at
// its start; the check does not dispose it. fileName is the uploader's label and proves nothing about the bytes.
// One check is registered per kind, with [RegisterSingleton(For = typeof(IAssetCheck), OfCollection = true)].
public interface IAssetCheck
{
    TemplateAssetKind Kind { get; }

    Task<AssetCheckResult> CheckAsync(Stream content, string fileName, CancellationToken ct);
}
