using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls;

// get_poll_results (V9): the poll's question and current options, and for each audience session that ran a round of it, the
// rounds that were opened, oldest first. Each round carries the option texts its counts belong to: a closed round's from its
// Final snapshot (so a later reword, reorder or shrink of the slide never relabels it), an open round's the slide's current
// options with its live tally, padded as the tally pads after an option was added. Counts has as many entries as the round's
// Options. A draft and a discarded draft are not listed. rate is answered over joined, not rounded, 0 when nobody had
// joined (joined is the roster's count when the round closed, or now for an open round). Totals only: no answer row is read,
// so who picked what never leaves the database (D5), and neither does the correct answer, which is not in the view. Needs
// edit access (404 "Deck not found" otherwise). Per request. Not named PollResults: that is the results setting's enum, in
// SlideWorks.Core.
[RegisterPerRequest]
public class PollResultsReader(AppDbContext db, PollReader reader, IPollTallies tallies, IAudienceRoster roster, IDeckAccess access, ICurrentActor actor)
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
            .Where(r => r.SlideId == row.Id && r.OpenedAt != null)
            .ToListAsync();
        var sessionIds = rounds.Select(r => r.SessionId).Distinct().ToList();
        var starts = await db.AudienceSessions.AsNoTracking().Where(s => sessionIds.Contains(s.Id)).Select(s => new { s.Id, s.StartedAt }).ToListAsync();
        var sessions = new List<PollResultsSession>();
        foreach (var session in starts.OrderBy(s => s.StartedAt))
        {
            var listed = new List<PollResultsRound>();
            foreach (var round in rounds.Where(r => r.SessionId == session.Id).OrderBy(r => r.Number))
            {
                listed.Add(round.ClosedAt != null ? ClosedRound(round) : await OpenRoundAsync(round, options));
            }
            sessions.Add(new PollResultsSession(session.StartedAt, listed));
        }
        return new PollResultsView(question, options, sessions);
    }

    private static PollResultsRound ClosedRound(PollRound round)
    {
        var final = round.Final!;
        var joined = round.JoinedAtClose ?? 0;
        return new PollResultsRound(round.Number, final.Options, final.Answered, joined, Rate(final.Answered, joined), final.Counts);
    }

    private async Task<PollResultsRound> OpenRoundAsync(PollRound round, IReadOnlyList<string> options)
    {
        var tally = await tallies.ReadAsync(round.Id.ToString(), options.Count);
        var joined = await roster.CountAsync(round.SessionId.ToString());
        return new PollResultsRound(round.Number, options, tally.Answered, joined, Rate(tally.Answered, joined), tally.Counts);
    }

    private static double Rate(int answered, int joined) => joined == 0 ? 0 : (double)answered / joined;
}
