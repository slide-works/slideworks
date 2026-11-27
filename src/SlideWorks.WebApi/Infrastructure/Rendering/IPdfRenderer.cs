namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The PDF of a deck, one page per slide, in front of IRenderEngine. It fails as the engine does, unchanged:
// RendererUnavailableException, TimeoutException, RenderingFailedException.
public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(string deckId, CancellationToken ct);
}
