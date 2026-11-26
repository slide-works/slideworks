namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The headless browser behind CachingCardRenderer (L9). It renders the SPA's /card/{id} or /card/generic route at
// 1200x630 and returns the PNG bytes. It throws RendererUnavailableException when no browser can run, and
// TimeoutException when a render takes too long.
// MeasureFontAsync also throws InvalidDataException when the browser cannot decode the bytes as a font.
public interface IRenderEngine
{
    Task<byte[]> RenderCardAsync(string deckId, CancellationToken ct);

    Task<byte[]> RenderGenericCardAsync(CancellationToken ct);

    // One slide of a deck, at 1920x1080, from the render route, with the findings its page measured. The same failures as a card render.
    Task<RenderedSlide> RenderSlideAsync(string deckId, int slide, CancellationToken ct);

    // The deck as a PDF, one page per slide, from the print page. The same failures as a card render.
    Task<byte[]> RenderPdfAsync(string deckId, CancellationToken ct);

    // The raw width of FontMeasurePage's sample line in a woff2 font: pixels / (characters x 16). One measure per uploaded font.
    Task<double> MeasureFontAsync(byte[] woff2, CancellationToken ct);
}
