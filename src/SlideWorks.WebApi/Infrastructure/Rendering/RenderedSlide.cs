using SlideWorks.WebApi.Features.Checks;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// One rendered slide: its PNG, and the findings the render page measured (the data-checks attribute).
public sealed record RenderedSlide(byte[] Png, IReadOnlyList<SlideFinding> Findings);
