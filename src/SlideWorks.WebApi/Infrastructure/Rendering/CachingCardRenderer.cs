using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The one ICardRenderer (L9). It sits in front of IRenderEngine.
// TODO: replace the pass-through with the card cache: read the deck's revision, look and sharing, key the card with
// RenderKey.For, keep PNGs on the data volume per key, and render the generic card at most once per process
// (single-flight). Until then every call renders, and a deck's card is keyed by its id alone.
[RegisterSingleton(For = typeof(ICardRenderer))]
public sealed class CachingCardRenderer(IRenderEngine engine) : ICardRenderer
{
    public async Task<RenderedCard> RenderCardAsync(string deckId, CancellationToken ct) =>
        new(await engine.RenderCardAsync(deckId, ct), deckId);

    public async Task<RenderedCard> RenderGenericCardAsync(CancellationToken ct) =>
        new(await engine.RenderGenericCardAsync(ct), RenderKey.GENERIC);
}
