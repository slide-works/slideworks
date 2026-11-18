using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
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
        var round = await OpenRoundAsync(Guid.Parse(session));
        return QaRoomRules.For(settings, slideSwitch, new QaRoundState(round, round is null ? 0 : revisions.Current(round)));
    }

    // The switch of the slide the guests see, or QaSlideSwitch.On when none is on screen.
    private async Task<QaSlideSwitch> SwitchOnScreenAsync(string deckId)
    {
        if (await presentations.GetAsync(deckId) is not { } presentation)
        {
            return QaSlideSwitch.On;
        }
        // One read of the deck's slide ids, so a slide deleted while live can't fall between a count and a lookup.
        var ids = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .Select(s => s.Id)
            .ToListAsync();
        if (ids.Count == 0)
        {
            return QaSlideSwitch.On;
        }
        return await rows.SwitchAsync(ids[Math.Clamp(presentation.Slide, 1, ids.Count) - 1]);
    }

    // The session's open Q&A round's id, or null when it has none. Untracked: this path only reads.
    private async Task<string?> OpenRoundAsync(Guid sessionId)
        => (await db.PollRounds.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.Kind == PollKind.Qa && r.ClosedAt == null)
            .Select(r => (Guid?)r.Id)
            .SingleOrDefaultAsync())?.ToString();
}
