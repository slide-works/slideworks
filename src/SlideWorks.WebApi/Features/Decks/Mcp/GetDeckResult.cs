using System.Text.Json.Serialization;
using SlideWorks.Core.Qa;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Translations.Mcp;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// get_deck: the whole deck for the agent that owns it (K6, L7), with its look, its talk length in minutes (P7, or null)
// and the live slides in order. url is absolute. Qa is the deck's Q&A settings (Q1; the defaults when none were changed) and
// Reactions the reaction totals of each audience session that has any, newest first (Q10).
public sealed record GetDeckResult(
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
    IReadOnlyList<GetDeckSlide> Slides,
    QaSettings Qa,
    IReadOnlyList<GetDeckReactionSession> Reactions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TranslationsSettingsView? Translations = null)
{
    public static GetDeckResult From(Deck deck, IEnumerable<Slide> slides, ResolvedTemplate template, string url, QaSettings qa, IReadOnlyList<GetDeckReactionSession> reactions, TranslationsSettingsView? translations, Func<Slide, IReadOnlyList<LineView>?> linesOf)
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
            slides.Select(slide => GetDeckSlide.From(slide, template, linesOf(slide))).ToList(),
            qa,
            reactions,
            translations);
}
