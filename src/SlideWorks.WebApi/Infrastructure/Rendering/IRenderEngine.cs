namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The headless browser behind CachingCardRenderer (L9). It renders the SPA's /card/{id} or /card/generic route at
// 1200x630 and returns the PNG bytes. It throws RendererUnavailableException when no browser can run, and
// TimeoutException when a render takes too long.
public interface IRenderEngine
{
    Task<byte[]> RenderCardAsync(string deckId, CancellationToken ct);

    Task<byte[]> RenderGenericCardAsync(CancellationToken ct);
}
