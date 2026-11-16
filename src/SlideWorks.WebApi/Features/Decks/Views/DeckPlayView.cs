using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Decks.Views;

// GET /api/play/{id}: everything the player needs in one request (deck-play.json, K4). The same shape as the sample
// deck (K3). It never contains notes.
public sealed record DeckPlayView(string Id, string Slug, string Title, string? Author, IReadOnlyList<string> Parts, IReadOnlyList<DeckPlaySlide> Slides)
{
    public static DeckPlayView From(Deck deck, IEnumerable<Slide> slides)
        => new(deck.Id, deck.Slug, deck.Title, deck.Author, DeckParts.Read(deck), slides.Select(DeckPlaySlide.From).ToList());
}
