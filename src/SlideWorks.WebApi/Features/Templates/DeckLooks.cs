using Agoda.IoC.Core;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Templates;

// IDeckLooks over the built-in catalogues: each method is LookViews' static of the same name.
[RegisterPerRequest(For = typeof(IDeckLooks))]
public sealed class DeckLooks(ITemplateCatalogue templates, IPaletteCatalogue palettes) : IDeckLooks
{
    public Task<LookView> ForAsync(Deck deck, CancellationToken ct) => Task.FromResult(LookViews.From(deck, templates, palettes));

    public Task<DeckLookView> ForPageAsync(Deck deck, CancellationToken ct) => Task.FromResult(LookViews.ForPage(deck, templates, palettes));

    public Task<LookView> DefaultAsync(DateTimeOffset now, CancellationToken ct) => Task.FromResult(LookViews.Default(templates, palettes, now));
}
