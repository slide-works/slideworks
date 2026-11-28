using System.Text.Json;
using System.Text.Json.Serialization;
using SlideWorks.Core.Qa;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Translations.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// get_deck with detail "summary": the same deck, qa and reactions included, with each slide reduced to its number, layout, part and headline.
public sealed record GetDeckSummary(
    string Id,
    string Slug,
    string Title,
    string? Subtitle,
    string? Author,
    IReadOnlyList<DeckPart> Parts,
    DeckSharing Sharing,
    string Url,
    GetDeckLook Look,
    [property: JsonPropertyName("talk_minutes")] int? TalkMinutes,
    IReadOnlyList<GetDeckSummarySlide> Slides,
    QaSettings Qa,
    IReadOnlyList<GetDeckReactionSession> Reactions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TranslationsSettingsView? Translations = null)
{
    public static GetDeckSummary From(Deck deck, IEnumerable<Slide> slides, ResolvedTemplate template, string url, QaSettings qa, IReadOnlyList<GetDeckReactionSession> reactions, TranslationsSettingsView? translations, Func<Slide, IReadOnlyList<LineView>?> linesOf)
        => new(
            deck.Id,
            deck.Slug,
            deck.Title,
            deck.Subtitle,
            deck.Author,
            DeckParts.Read(deck),
            deck.Sharing,
            url,
            GetDeckLook.From(deck, template),
            deck.TalkMinutes,
            slides.Select(slide => GetDeckSummarySlide.From(slide, linesOf(slide))).ToList(),
            qa,
            reactions,
            translations);
}

// Lines is each line's state in the language get_deck was asked for (SW-883 K4), left out when no translated language was asked.
public sealed record GetDeckSummarySlide(
    int Number,
    string Layout,
    int? Part,
    string Headline,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<LineView>? Lines = null)
{
    // A summary headline is cut to this many characters (SW-568), so a long quote does not make the short read long.
    public const int HEADLINE_MAX_LENGTH = 120;

    public static GetDeckSummarySlide From(Slide slide, IReadOnlyList<LineView>? lines)
        => new(slide.Position + 1, slide.Layout, slide.Part, Cut(HeadlineOf(SlideJson.Slots(slide)) ?? ""), lines);

    private static string Cut(string headline)
        => headline.Length <= HEADLINE_MAX_LENGTH ? headline : headline[..HEADLINE_MAX_LENGTH];

    // The line a summary shows: the headline, else the title, the question, or the quote. A slide with none of them
    // shows "".
    private static string? HeadlineOf(JsonElement slots)
    {
        foreach (var name in new[] { "headline", "title", "question" })
        {
            if (slots.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        if (!slots.TryGetProperty("quote", out var quote))
        {
            return null;
        }

        return quote.ValueKind switch
        {
            JsonValueKind.String => quote.GetString(),
            JsonValueKind.Object when quote.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String => text.GetString(),
            _ => null,
        };
    }
}
