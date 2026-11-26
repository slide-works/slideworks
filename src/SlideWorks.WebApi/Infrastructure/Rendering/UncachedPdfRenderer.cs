using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// Passes every request straight to the engine and returns its bytes.
// TODO: the PDF cache replaces this class.
[RegisterSingleton(For = typeof(IPdfRenderer))]
public sealed class UncachedPdfRenderer(IRenderEngine engine) : IPdfRenderer
{
    public Task<byte[]> RenderAsync(string deckId, CancellationToken ct) => engine.RenderPdfAsync(deckId, ct);
}
