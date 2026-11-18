using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// create_deck: { deck, url, sharing } (K6). deck is the new deck's id, url its absolute player link, and sharing
// "private" for a new deck. look names the template and palette it got (L7) and the version it is pinned to, and layouts, outline and house_style come
// from that template, so the agent can plan the deck without another call.
public sealed record CreateDeckToolResult(
    string Deck,
    string Url,
    DeckSharing Sharing,
    CreateDeckLook Look,
    IReadOnlyList<string> Layouts,
    IReadOnlyList<string> Outline,
    [property: JsonPropertyName("house_style")] IReadOnlyList<string> HouseStyle)
{
    public static CreateDeckToolResult From(Deck deck, string url, ResolvedTemplate template)
        => new(
            deck.Id,
            url,
            deck.Sharing,
            new CreateDeckLook(deck.Template, deck.Palette, deck.TemplateVersion),
            template.Template.Layouts,
            template.Template.Outline,
            template.Template.HouseStyle);
}
