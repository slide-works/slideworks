namespace SlideWorks.WebApi.Infrastructure.Rendering;

// The share card (L9): a 1200x630 PNG of a deck's title, rendered on the server because link previews happen with no
// agent present (D21). Callers use this, never IRenderEngine. An unknown, private or unlisted deck never starts a
// render: callers send those to RenderGenericCardAsync.
public interface ICardRenderer
{
    Task<RenderedCard> RenderCardAsync(string deckId, CancellationToken ct);

    Task<RenderedCard> RenderGenericCardAsync(CancellationToken ct);
}
