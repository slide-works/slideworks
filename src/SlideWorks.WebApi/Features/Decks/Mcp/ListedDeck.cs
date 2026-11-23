using SlideWorks.WebApi.Features.Decks.Views;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One deck in list_decks. Id is what every other tool takes as deck; Url is the absolute player link; UpdatedLabel says
// when it last changed ("2 min ago"). Access is "owner" or "write" (C4).
public sealed record ListedDeck(string Id, string Slug, string Title, string? Subtitle, int SlideCount, DeckSharing Sharing, string Url, string UpdatedLabel, string Access)
{
    // TODO: access is hardcoded to "owner"; S1 replaces it with the caller's real access.
    public static ListedDeck From(DeckRow row, string url)
        => new(row.Id, row.Slug, row.Title, row.Subtitle, row.SlideCount, row.Sharing, url, row.UpdatedLabel, "owner");
}
