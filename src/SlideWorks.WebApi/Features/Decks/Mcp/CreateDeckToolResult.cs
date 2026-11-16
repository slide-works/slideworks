namespace SlideWorks.WebApi.Features.Decks.Mcp;

// create_deck: { deck, url, sharing } (K6). deck is the new deck's id, url its absolute player link, and sharing
// "private" for a new deck.
public sealed record CreateDeckToolResult(string Deck, string Url, DeckSharing Sharing)
{
    public static CreateDeckToolResult From(Deck deck, string url)
        => new(deck.Id, url, deck.Sharing);
}
