namespace SlideWorks.WebApi.Infrastructure.Identity;

// The absolute URLs SlideWorks hands out: links to decks, the MCP endpoint and the OAuth metadata. They come from
// SLIDEWORKS_PUBLIC_URL, never from a request's Host header, so a forged Host can't reach a link or OAuth metadata.
public interface IPublicUrls
{
    // The origin people and agents reach SlideWorks at, with no trailing slash, such as https://slideworks.example.
    string Base { get; }

    // {Base}/mcp, the MCP endpoint and the one OAuth resource.
    string Mcp { get; }

    // {Base}/d/{id}/{slug}, plus #{slide} or #{slide}.{step} when given.
    string Deck(string id, string slug, int? slide = null, int? step = null);
}
