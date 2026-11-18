using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Qa;

// The real IAudienceRoom (Q1, Q3): what the room may do on the slide on screen, from QaRoomRules.For over the deck's
// settings, the switch of the presenter's slide in SW-4's presentation state, the session's open Q&A round and its revision
// in QaRevisions. Null outside phase live. With no presentation, or an empty deck, no slide is on screen and the deck's
// settings apply as they are. A position past the end of the deck (slides removed while live) is the last slide, the rule
// AudienceFeed.Project and AudienceActivity use, so the room matches the slide on the phone.
//
// The feed calls it under its per-deck gate on every compose, so it only reads, with indexed reads, and never publishes.
// Per request, because it uses the database.
[RegisterPerRequest(For = typeof(IAudienceRoom))]
public sealed class AudienceRoom(
    AppDbContext db,
    IAudienceSessions sessions,
    IPresentations presentations,
    QaRows rows,
    PollRows polls,
    QaRevisions revisions) : IAudienceRoom
{
    public async Task<QaRoom?> ForDeckAsync(string deckId)
    {
        if (await sessions.ForDeckAsync(deckId) is not { Phase: AudiencePhase.Live, SessionId: { } session })
        {
            return null;
        }
        var settings = await rows.SettingsAsync(deckId);
        var slideSwitch = await SwitchOnScreenAsync(deckId);
        var round = (await polls.OpenQaRoundAsync(Guid.Parse(session)))?.Id.ToString();
        return QaRoomRules.For(settings, slideSwitch, new QaRoundState(round, round is null ? 0 : revisions.Current(round)));
    }

    // The switch of the slide the guests see, or QaSlideSwitch.On when none is on screen.
    private async Task<QaSlideSwitch> SwitchOnScreenAsync(string deckId)
    {
        if (await presentations.GetAsync(deckId) is not { } presentation)
        {
            return QaSlideSwitch.On;
        }
        var count = await db.Slides.AsNoTracking().CountAsync(s => s.DeckId == deckId && s.DeletedAt == null);
        if (count == 0)
        {
            return QaSlideSwitch.On;
        }
        var shown = Math.Clamp(presentation.Slide, 1, count);
        var slideId = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == shown - 1)
            .Select(s => s.Id)
            .SingleAsync();
        return await rows.SwitchAsync(slideId);
    }
}
