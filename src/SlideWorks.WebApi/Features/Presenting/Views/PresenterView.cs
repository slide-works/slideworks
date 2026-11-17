using SlideWorks.WebApi.Features.Look.Views;

namespace SlideWorks.WebApi.Features.Presenting.Views;

// GET /api/decks/{id}/presenter (P3): the presenter window's and the phone's one request, for the deck's owner only. The
// play view's fields plus each slide's notes and headline, the talk length (P7), the audience window's absolute URL
// ({Base}/d/{id}/{slug}?present) and the phone's (RemoteUrl, {Base}/remote?deck={id}, which the QR encodes).
public sealed record PresenterView(
    string Id,
    string Slug,
    string Title,
    string? Author,
    IReadOnlyList<string> Parts,
    LookView Look,
    IReadOnlyList<PresenterSlide> Slides,
    int? TalkMinutes,
    string AudienceUrl,
    string RemoteUrl);
