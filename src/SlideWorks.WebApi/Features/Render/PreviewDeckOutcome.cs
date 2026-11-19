using SlideWorks.WebApi.Features.Render.Mcp;

namespace SlideWorks.WebApi.Features.Render;

// The preview the tool returns: the small JSON result, and the PNG of each slide that rendered.
public sealed record PreviewDeckOutcome(PreviewDeckResult Result, IReadOnlyList<byte[]> Images);