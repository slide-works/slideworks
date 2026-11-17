using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Polls;

// get_poll_results (V9): the poll's question and current options, and for each audience session that ran a round of it, the
// rounds that were opened and then closed, oldest first, each from its Final: how many answered, how many had joined when it
// closed, the response rate (answered over joined, not rounded; 0 when nobody had joined) and the count per option. A round
// that is still open, a draft and a discarded draft are not listed. Totals only: no answer row is read, so who picked what
// never leaves the database (D5), and neither does the correct answer, which is not in the view. A round that closed before
// an option was added has fewer counts; they are padded with zeros so counts line up with options. Needs edit access (404
// "Deck not found" otherwise). Per request. Not named PollResults: that is the results setting's enum, in SlideWorks.Core.
[RegisterPerRequest]
public class PollResultsReader(AppDbContext db, PollReader reader, IDeckAccess access, ICurrentActor actor)
{
    public async Task<PollResultsView> ReadAsync(string deckId, int slide)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var count = await db.Slides.CountAsync(s => s.DeckId == deckId && s.DeletedAt == null);
        if (slide < 1 || slide > count)
        {
            throw new BadRequestException(count == 0 ? "slide: the deck has no slides" : $"slide: must be between 1 and {count}");
        }
        var (row, _) = await reader.SlideAsync(deckId, slide) ?? throw new BadRequestException($"slide: slide {slide} is not a poll");
        var (question, options) = PollReader.SlotsOf(row.SlotsJson);

        var rounds = await db.PollRounds.AsNoTracking()
            .Where(r => r.SlideId == row.Id && r.OpenedAt != null && r.ClosedAt != null)
            .ToListAsync();
        var sessionIds = rounds.Select(r => r.SessionId).Distinct().ToList();
        var starts = await db.AudienceSessions.AsNoTracking().Where(s => sessionIds.Contains(s.Id)).Select(s => new { s.Id, s.StartedAt }).ToListAsync();
        var sessions = starts.OrderBy(s => s.StartedAt).Select(session => new PollResultsSession(
            session.StartedAt,
            [.. rounds.Where(r => r.SessionId == session.Id).OrderBy(r => r.Number).Select(r => RoundOf(r, options.Count))])).ToList();
        return new PollResultsView(question, options, sessions);
    }

    private static PollResultsRound RoundOf(PollRound round, int optionCount)
    {
        var final = round.Final!;
        var joined = round.JoinedAtClose ?? 0;
        var counts = final.Counts.Concat(Enumerable.Repeat(0, Math.Max(0, optionCount - final.Counts.Count))).ToList();
        return new PollResultsRound(round.Number, final.Answered, joined, joined == 0 ? 0 : (double)final.Answered / joined, counts);
    }
}
