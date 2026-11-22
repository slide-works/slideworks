using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Bundles;

// One file an import will store: where the manifest names it (Path, a file in the bundle or a custom layout's stem),
// what it is, the label it is stored under, and how to read its bytes from the bundle again.
internal sealed record BundleFile(string Path, TemplateAssetKind Kind, string FileName, Func<CancellationToken, Task<byte[]>> Load);
