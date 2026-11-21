namespace SlideWorks.WebApi.Features.Look.Views;

// GET /api/render/{id}/card (card.json): what the share card shows. A generic card (any deck whose sharing isn't link,
// D12) names nothing: kicker, title, subtitle and author are null, slideCount is 0 and the look is the default one.
// Kicker is the deck's opening slide's kicker, null when it has none. Host is the public URL's host (with its port when
// that isn't the default), shown at the card's bottom right on every card.
public sealed record CardView(bool Generic, string? Kicker, string? Title, string? Subtitle, string? Author, int SlideCount, string Host, LookView Look)
{
    public static CardView ForDeck(string? kicker, string title, string? subtitle, string? author, int slideCount, string host, LookView look)
        => new(false, kicker, title, subtitle, author, slideCount, host, look);

    public static CardView GenericWith(string host, LookView defaultLook) => new(true, null, null, null, null, 0, host, defaultLook);
}
