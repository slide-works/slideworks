using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Qa.Mcp;

// get_questions (Q10): for each audience session that ran a Q&A round, newest session first, how many questions were asked,
// answered and hidden and the questions in the order they were asked (by round, then number). It is a report over the stored
// rows, not the live view: it still lists a past session's questions when the deck's questions are now off, and it needs no
// round to be open. A question's state is QuestionStatuses.Of (hidden wins). Its votes are the round's tally, read with the
// round's own option count (a round open or closed; a closed round's tally is rebuilt from its answers). state keeps only the
// questions in that state; a session's counts always cover all of its questions. The Question.GuestId and every answer row
// stay in the database: no guest id is read, so none can be returned. Needs edit access (404 "Deck not found" otherwise).
// Per request.
[RegisterPerRequest]
public class QuestionsReports(AppDbContext db, QaAgentAccess access, IPollTallies tallies)
{
    private static readonly Dictionary<string, QuestionStatus> STATES = new(StringComparer.Ordinal)
    {
        ["open"] = QuestionStatus.Open,
        ["waiting"] = QuestionStatus.Waiting,
        ["answered"] = QuestionStatus.Answered,
        ["hidden"] = QuestionStatus.Hidden,
    };

    public async Task<QuestionsReport> ReadAsync(string deckId, string? state)
    {
        await access.RequireEditorAsync(deckId);
        var only = ParseState(state);

        var rounds = await db.PollRounds.AsNoTracking()
            .Where(r => r.Kind == PollKind.Qa)
            .Join(db.Polls.AsNoTracking().Where(p => p.DeckId == deckId && p.Kind == PollKind.Qa), r => r.PollId, p => p.Id, (r, _) => new { r.Id, r.SessionId, r.Number })
            .ToListAsync();
        var sessionIds = rounds.Select(r => r.SessionId).Distinct().ToList();
        var starts = await db.AudienceSessions.AsNoTracking().Where(s => sessionIds.Contains(s.Id)).Select(s => new { s.Id, s.StartedAt }).ToListAsync();
        var sessions = new List<QuestionsReportSession>();
        foreach (var session in starts.OrderByDescending(s => s.StartedAt))
        {
            var all = new List<QuestionsReportQuestion>();
            foreach (var round in rounds.Where(r => r.SessionId == session.Id).OrderBy(r => r.Number))
            {
                all.AddRange(await QuestionsOfAsync(round.Id));
            }
            sessions.Add(new QuestionsReportSession(
                session.StartedAt,
                all.Count,
                all.Count(q => q.State == QuestionStatus.Answered),
                all.Count(q => q.State == QuestionStatus.Hidden),
                [.. all.Where(q => only == null || q.State == only)]));
        }
        return new QuestionsReport(sessions);
    }

    private static QuestionStatus? ParseState(string? state)
    {
        if (state == null)
        {
            return null;
        }
        return STATES.TryGetValue(state, out var known) ? known : throw new BadRequestException("state: must be one of 'open', 'waiting', 'answered', 'hidden'");
    }

    private async Task<IEnumerable<QuestionsReportQuestion>> QuestionsOfAsync(Guid roundId)
    {
        var questions = await db.Questions.AsNoTracking().Where(q => q.RoundId == roundId).OrderBy(q => q.Position)
            .Select(q => new { q.Position, q.Name, q.State, Hidden = q.HiddenAt != null, q.AskedAt })
            .ToListAsync();
        var texts = await db.PollOptions.AsNoTracking().Where(o => o.RoundId == roundId).ToDictionaryAsync(o => o.Position, o => o.Text);
        var counts = (await tallies.ReadAsync(roundId.ToString(), texts.Count)).Counts;
        return questions.Select(q => new QuestionsReportQuestion(texts[q.Position], q.Name, QuestionStatuses.Of(q.State, q.Hidden), counts[q.Position], q.AskedAt));
    }
}
