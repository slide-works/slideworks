namespace SlideWorks.WebApi.Features.Look.Views;

// GET /api/render/{id}/card (card.json): what the share card shows. A generic card (any deck whose sharing isn't link,
// D12) names nothing: title, subtitle and author are null, slideCount is 0 and the look is the default one.
public sealed record CardView(bool Generic, string? Title, string? Subtitle, string? Author, int SlideCount, LookView Look)
{
    public static CardView ForDeck(string title, string? subtitle, string? author, int slideCount, LookView look)
        => new(false, title, subtitle, author, slideCount, look);

    public static CardView GenericWith(LookView defaultLook) => new(true, null, null, null, 0, defaultLook);
}
