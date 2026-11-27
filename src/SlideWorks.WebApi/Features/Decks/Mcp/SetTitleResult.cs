using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// set_title's result: the deck's title, subtitle, author and slug after the change.
public sealed record SetTitleResult(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("subtitle")] string? Subtitle,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("slug")] string Slug)
{
    public static SetTitleResult From(DeckTitle title) => new(title.Title, title.Subtitle, title.Author, title.Slug);
}
