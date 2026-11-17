using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Polls.Data;

// The poll reads every poll service shares, and the settings row a poll slide gets (V1, V2). Each runs on the request's
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

    // The session's one round without ClosedAt, draft or open, or null. One read of the partial unique index. The row is
    // tracked, so a caller may change it and save.
    public Task<PollRound?> OpenRoundAsync(Guid sessionId)
        => db.PollRounds.SingleOrDefaultAsync(r => r.SessionId == sessionId && r.ClosedAt == null);

    // The slide's poll settings, or null when the slide has never been a poll.
    public async Task<PollSettings?> SettingsAsync(Guid slideId)
        => (await db.Polls.AsNoTracking().SingleOrDefaultAsync(p => p.SlideId == slideId))?.ToSettings();
}
