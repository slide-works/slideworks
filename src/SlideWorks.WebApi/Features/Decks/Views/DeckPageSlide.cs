using System.Text.Json;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Decks.Views;

// One slide on the deck page, notes included (the page is the owner's). Number is 1-based. Headline is the slide's
// one-line name in the list: its headline slot where the layout has one, title.title, divider.title, or the first 80
// characters of quote.quote; null only when the layout has no text that fits. Link is root-relative:
// /d/{id}/{slug}#{number}. Slots are the stored slots, whose shape depends on the layout. Notes read with the deep-dive
// prefix while DeepDive is set (D16).
public sealed record DeckPageSlide(
    int Number,
    string Layout,
    int? Part,
    bool DeepDive,
    string? Headline,
    int Steps,
    string? Notes,
    string Link,
    JsonElement Slots)
{
    private const int QUOTE_HEADLINE_LENGTH = 80;

    public static DeckPageSlide From(Deck deck, Slide slide)
    {
        var number = slide.Position + 1;
        var slots = SlideJson.Slots(slide);
        return new DeckPageSlide(
            number,
            slide.Layout,
            slide.Part,
            slide.DeepDive,
            HeadlineOf(slide.Layout, slots),
            slide.Steps,
            DeckNotes.Of(slide),
            DeckLinks.Slide(deck.Id, deck.Slug, number),
            slots);
    }

    // Also the presenter view's headline (PresenterSlide), so the two lists name a slide the same way.
    public static string? HeadlineOf(string layout, JsonElement slots) => layout switch
    {
        "title" or "divider" => Text(slots, "title"),
        "quote" => Shorten(Text(slots, "quote")),
        "poll" => Text(slots, "question"),
        _ => Text(slots, "headline"),
    };

    private static string? Text(JsonElement slots, string name) =>
        slots.ValueKind == JsonValueKind.Object
        && slots.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // The first 80 UTF-16 code units, stepping back one rather than splitting a surrogate pair.
    private static string? Shorten(string? text)
    {
        if (text == null || text.Length <= QUOTE_HEADLINE_LENGTH)
        {
            return text;
        }
        var length = char.IsHighSurrogate(text[QUOTE_HEADLINE_LENGTH - 1]) ? QUOTE_HEADLINE_LENGTH - 1 : QUOTE_HEADLINE_LENGTH;
        return text[..length];
    }
}
