using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Render.Print;

namespace SlideWorks.WebApi.Features.Qa;

// The Q&A results the print view carries (deck-print.json "qa"): the newest Q&A round of the session, or null when the session
// had none. Phase is "closed" once the round's ClosedAt is set, else "open". Counted questions are open or answered by
// QuestionStatuses.Of (hidden, and waiting for approval, are left out). Votes are the round's tally read with the round's own
// option count, as QuestionsReports does. Only the number, text and votes are read: no guest id or name. Per request.
[RegisterPerRequest(For = typeof(IPrintQaResults))]
public class PrintQaResults(AppDbContext db, IPollTallies tallies) : IPrintQaResults
{
    private const int TOP = 5;

    public async Task<PrintQa?> ReadAsync(string deckId, Guid sessionId, CancellationToken ct)
    {
        var round = await db.PollRounds.AsNoTracking()
            .Where(r => r.Kind == PollKind.Qa && r.SessionId == sessionId)
            .Join(db.Polls.AsNoTracking().Where(p => p.DeckId == deckId && p.Kind == PollKind.Qa), r => r.PollId, p => p.Id, (r, _) => new { r.Id, r.Number, r.ClosedAt })
            .OrderByDescending(r => r.Number)
            .FirstOrDefaultAsync(ct);
        if (round == null)
        {
            return null;
        }

        var questions = await db.Questions.AsNoTracking().Where(q => q.RoundId == round.Id)
            .Select(q => new { q.Position, q.State, Hidden = q.HiddenAt != null })
            .ToListAsync(ct);
        var texts = await db.PollOptions.AsNoTracking().Where(o => o.RoundId == round.Id).ToDictionaryAsync(o => o.Position, o => o.Text, ct);
        var counts = (await tallies.ReadAsync(round.Id.ToString(), texts.Count)).Counts;
        var counted = questions
            .Where(q => QuestionStatuses.Of(q.State, q.Hidden) is QuestionStatus.Open or QuestionStatus.Answered)
            .Select(q => new PrintQaQuestion(q.Position + 1, texts[q.Position], counts[q.Position]))
            .ToList();
        return new PrintQa(
            round.ClosedAt == null ? "open" : "closed",
            counted.Count,
            [.. counted.OrderByDescending(q => q.Votes).ThenBy(q => q.Number).Take(TOP)]);
    }
}
