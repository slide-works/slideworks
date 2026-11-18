using SlideWorks.Core.Look.CustomLayouts;

namespace SlideWorks.WebApi.Features.Templates.Layouts;

// One custom layout of a template version: the compiled layout, the asset that holds it, and what its owner's review
// said. Overflow and Contrast are the findings the owner accepted when approving; both are empty until then.
public sealed record TemplateLayout(CompiledLayout Layout, string AssetId, bool Approved, IReadOnlyList<string> Overflow, IReadOnlyList<string> Contrast);
