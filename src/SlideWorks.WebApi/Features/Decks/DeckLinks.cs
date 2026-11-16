namespace SlideWorks.WebApi.Features.Decks;

// Root-relative links to the player, for every link the SPA follows: /d/{id}/{slug}, and /d/{id}/{slug}#{slide} for one
// slide. Absolute links for agents come from IPublicUrls.Deck.
public static class DeckLinks
{
    public static string Play(string id, string slug) => $"/d/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(slug)}";

    public static string Slide(string id, string slug, int number) => $"{Play(id, slug)}#{number}";
}
