using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Join.Views;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Presenting.Views;

// GET /api/decks/{id}/presenter (P3): the presenter window's and the phone's one request, for the deck's owner only. The
// play view's fields plus each slide's notes and headline, the talk length (P7), the audience window's absolute URL
// ({Base}/d/{id}/{slug}?present) and the phone's (RemoteUrl, {Base}/remote?deck={id}, which the QR encodes).
public sealed record PresenterView(
    string Id,
    string Slug,
    string Title,
    string? Author,
    IReadOnlyList<DeckPart> Parts,
    LookView Look,
    JoinView Join,
    IReadOnlyList<PresenterSlide> Slides,
    int? TalkMinutes,
    string AudienceUrl,
    string RemoteUrl)
{
    public static PresenterView From(Deck deck, IEnumerable<Slide> slides, ResolvedTemplate template, LookView look, JoinView join, IPublicUrls urls)
        => new(
            deck.Id,
            deck.Slug,
            deck.Title,
            deck.Author,
            DeckParts.Read(deck),
            look,
            join,
            slides.Select(slide => PresenterSlide.From(slide, template)).ToList(),
            deck.TalkMinutes,
            $"{urls.Deck(deck.Id, deck.Slug)}?present",
            $"{urls.Base}/remote?deck={Uri.EscapeDataString(deck.Id)}");
}
