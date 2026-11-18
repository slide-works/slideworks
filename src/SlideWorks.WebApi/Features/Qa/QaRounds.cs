using Agoda.IoC.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Qa;

// The live session's Q&A round (Q2, Q6). Opening makes the deck's Q&A poll the first time (through Poll.ForQa, at most one
// per deck) and opens the session's next round of it, numbered from 1; it is idempotent and never throws for a round that is
// already open, because a throw on Go live would leave a session with no start push. When two calls race, the partial unique
// indexes (one open round per session per kind, one Q&A poll per deck) refuse the loser, which reads the winner's round and
// opens nothing. Closing writes ClosedAt, JoinedAtClose (the roster's count) and Final (each question's upvotes, by position)
// in one transaction, so the answer writer, which checks ClosedAt inside its own commit, has either counted an upvote into
// Final or refuses it. The deck's settings are read from its row here, never through IQaSettings: IQaSettings calls this
// seam, so the other way round would be a dependency cycle.
[RegisterPerRequest(For = typeof(IQaRounds))]
public class QaRounds(
    AppDbContext db,
    PollRows pollRows,
    QaRows qaRows,
    IAudienceSessions sessions,
    IAudienceRoster roster,
    TimeProvider clock) : IQaRounds
{
    // SQLITE_CONSTRAINT_UNIQUE: the extended code, so a NOT NULL or primary key failure is never mistaken for the race.
    private const int SQLITE_CONSTRAINT_UNIQUE = 2067;

    // A race costs one attempt; the next attempt finds the winner's round.
    private const int OPEN_ATTEMPTS = 3;

    public async Task OpenAsync(string deckId, Guid sessionId)
    {
        if ((await qaRows.SettingsAsync(deckId)).Questions == QaQuestions.Off)
        {
            return;
        }
        await OpenRoundAsync(deckId, sessionId);
    }

    public async Task CloseAsync(string deckId, Guid sessionId)
    {
        var joined = await roster.CountAsync(sessionId.ToString());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var round = await pollRows.OpenQaRoundAsync(sessionId);
        if (round == null)
        {
            return;
        }
        var texts = await db.PollOptions.AsNoTracking()
            .Where(o => o.RoundId == round.Id)
            .OrderBy(o => o.Position)
            .Select(o => o.Text)
            .ToListAsync();
        var answers = await db.PollAnswers.AsNoTracking().Where(a => a.RoundId == round.Id).Select(a => a.Picks).ToListAsync();
        var counts = new int[texts.Count];
        foreach (var position in answers.SelectMany(picks => picks).Where(p => p >= 0 && p < counts.Length))
        {
            counts[position]++;
        }
        round.ClosedAt = clock.GetUtcNow();
        round.JoinedAtClose = joined;
        round.Final = new PollFinal(texts, counts, answers.Count(picks => picks.Count > 0));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task EnsureAsync(string deckId)
    {
        if (await sessions.ForDeckAsync(deckId) is { Phase: AudiencePhase.Live, SessionId: { } sessionId })
        {
            await OpenRoundAsync(deckId, Guid.Parse(sessionId));
        }
    }

    private async Task OpenRoundAsync(string deckId, Guid sessionId)
    {
        for (var attempt = 1; attempt <= OPEN_ATTEMPTS; attempt++)
        {
            if (await pollRows.OpenQaRoundAsync(sessionId) != null)
            {
                return;
            }
            var poll = await pollRows.QaPollAsync(deckId);
            var newPoll = poll == null;
            poll ??= db.Polls.Add(Poll.ForQa(deckId)).Entity;
            var last = await db.PollRounds.AsNoTracking()
                .Where(r => r.PollId == poll.Id && r.SessionId == sessionId)
                .MaxAsync(r => (int?)r.Number) ?? 0;
            var round = new PollRound
            {
                Id = Guid.NewGuid(),
                PollId = poll.Id,
                Kind = PollKind.Qa,
                SlideId = null,
                SessionId = sessionId,
                Number = last + 1,
                OpenedAt = clock.GetUtcNow(),
            };
            db.PollRounds.Add(round);
            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (attempt < OPEN_ATTEMPTS && IsUniqueClash(ex))
            {
                db.Entry(round).State = EntityState.Detached;
                if (newPoll)
                {
                    db.Entry(poll).State = EntityState.Detached;
                }
            }
        }
    }

    private static bool IsUniqueClash(DbUpdateException ex)
        => ex.InnerException is SqliteException { SqliteExtendedErrorCode: SQLITE_CONSTRAINT_UNIQUE };
}
