namespace SlideWorks.WebApi.Features.Decks.Views;

// POST /api/decks: 201 { id, slug, playUrl }. playUrl is root-relative (/d/{id}/{slug}), as every link the SPA follows.
public sealed record CreateDeckResult(string Id, string Slug, string PlayUrl)
{
    public static CreateDeckResult From(Deck deck) => new(deck.Id, deck.Slug, $"/d/{deck.Id}/{deck.Slug}");
}
