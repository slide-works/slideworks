using System.Text.Json.Serialization;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Slides;

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
    IReadOnlyList<string> Parts,
    DeckSharing Sharing,
    string Url,
    GetDeckLook Look,
    [property: JsonPropertyName("talk_minutes")] int? TalkMinutes,
    IReadOnlyList<GetDeckSlide> Slides,
    QaSettings Qa,
    IReadOnlyList<GetDeckReactionSession> Reactions)
{
    public static GetDeckResult From(Deck deck, IEnumerable<Slide> slides, string url, QaSettings qa, IReadOnlyList<GetDeckReactionSession> reactions)
        => new(
            deck.Id,
            deck.Slug,
            deck.Title,
            deck.Subtitle,
            deck.Author,
            DeckParts.Read(deck),
            deck.Sharing,
            url,
            GetDeckLook.From(deck),
            deck.TalkMinutes,
            slides.Select(GetDeckSlide.From).ToList(),
            qa,
            reactions);
}
