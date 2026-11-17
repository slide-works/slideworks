using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Polls.Data;

// The poll reads every poll service shares, and the settings row a poll slide gets (V1, V2; SW-9 Q2). Each runs on the request's
// context, so a call inside a caller's transaction is part of it.
[RegisterPerRequest]
public class PollRows(AppDbContext db)
{
    public const string LAYOUT = "poll";

    // Makes the slide's settings row with the defaults, unless it already has one (a slide that was a poll before keeps its
    // settings). Called inside the slide write's transaction when a slide is added as a poll or its layout becomes poll.
    public async Task EnsureAsync(Guid slideId, string deckId)
    {
        if (await db.Polls.AnyAsync(p => p.SlideId == slideId))
        {
            return;
        }
        db.Polls.Add(Poll.From(slideId, deckId, PollSettings.Default));
        await db.SaveChangesAsync();
    }

    // The session's one slide round without ClosedAt, draft or open, or null; a Q&A round is never it (SW-9 Q2). One read
    // of the partial unique index. The row is tracked, so a caller may change it and save.
    public Task<PollRound?> OpenRoundAsync(Guid sessionId)
        => db.PollRounds.SingleOrDefaultAsync(r => r.SessionId == sessionId && r.Kind == PollKind.Slide && r.ClosedAt == null);

    // The session's one Q&A round without ClosedAt, or null. Tracked, as OpenRoundAsync's.
    public Task<PollRound?> OpenQaRoundAsync(Guid sessionId)
        => db.PollRounds.SingleOrDefaultAsync(r => r.SessionId == sessionId && r.Kind == PollKind.Qa && r.ClosedAt == null);

    // The deck's Q&A poll, or null when it has none yet. Tracked.
    public Task<Poll?> QaPollAsync(string deckId)
        => db.Polls.SingleOrDefaultAsync(p => p.DeckId == deckId && p.Kind == PollKind.Qa);

    // The id of the slide's poll, for a new round on it. Throws when the slide has never been a poll.
    public Task<Guid> SlidePollIdAsync(Guid slideId)
        => db.Polls.Where(p => p.SlideId == slideId).Select(p => p.Id).SingleAsync();

    // The slide's poll settings, or null when the slide has never been a poll.
    public async Task<PollSettings?> SettingsAsync(Guid slideId)
        => (await db.Polls.AsNoTracking().SingleOrDefaultAsync(p => p.SlideId == slideId))?.ToSettings();

    // The settings of the round's poll, read by its PollId.
    public async Task<PollSettings> RoundSettingsAsync(PollRound round)
        => (await db.Polls.AsNoTracking().SingleAsync(p => p.Id == round.PollId)).ToSettings();
}
