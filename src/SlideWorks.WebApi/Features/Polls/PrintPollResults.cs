using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Render.Print;

namespace SlideWorks.WebApi.Features.Polls;

// The poll results of one session for the print view (P1 polls): for each live poll slide, its newest opened round in the
// session, a draft or discarded one never counted. A closed round's counts are its Final snapshot, an open one's the live
// tally, as the presenter's view reads them. A closed round whose snapshot option texts or order differ from the slide's
// current options is left out, so no bar is mislabelled. Replies and words are what PollControlView holds for the round:
// empty on a choice poll. Totals only: the answer rows are read for their text, never for who wrote it. Per request.
[RegisterPerRequest(For = typeof(IPrintPollResults))]
public class PrintPollResults(AppDbContext db, IPollTallies tallies) : IPrintPollResults
{
    public async Task<IReadOnlyList<PrintPoll>> ReadAsync(string deckId, Guid sessionId, CancellationToken ct)
    {
        var slides = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Layout == PollRows.LAYOUT)
            .OrderBy(s => s.Position)
            .Select(s => new { s.Id, s.Position, s.SlotsJson })
            .ToListAsync(ct);
        var slideIds = slides.Select(s => s.Id).ToList();
        var settings = await db.Polls.AsNoTracking()
            .Where(p => p.SlideId != null && slideIds.Contains(p.SlideId.Value))
            .ToDictionaryAsync(p => p.SlideId!.Value, p => p.ToSettings(), ct);
        var opened = await db.PollRounds.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.SlideId != null && slideIds.Contains(r.SlideId.Value) && r.OpenedAt != null)
            .ToListAsync(ct);

        var printed = new List<PrintPoll>();
        foreach (var slide in slides)
        {
            var newest = opened.Where(r => r.SlideId == slide.Id).MaxBy(r => r.Number);
            if (newest == null || !settings.TryGetValue(slide.Id, out var poll))
            {
                continue;
            }
            if (await PrintAsync(slide.Position + 1, newest, poll, slide.SlotsJson, ct) is { } result)
            {
                printed.Add(result);
            }
        }
        return printed;
    }

    private async Task<PrintPoll?> PrintAsync(int slide, PollRound round, PollSettings settings, string slotsJson, CancellationToken ct)
    {
        var (_, options) = PollReader.SlotsOf(slotsJson);
        var closed = round.ClosedAt != null;
        int answered;
        IReadOnlyList<int> counts;
        if (closed)
        {
            var final = round.Final!;
            if (!final.Options.SequenceEqual(options))
            {
                return null;
            }
            (answered, counts) = (final.Answered, final.Counts);
        }
        else
        {
            var tally = await tallies.ReadAsync(round.Id.ToString(), options.Count);
            (answered, counts) = (tally.Answered, tally.Counts);
        }

        var answerShown = closed && round.AnswerShownAt != null;
        var (replies, words) = await OpenOfAsync(settings.Answers, round.Id, ct);
        return new PrintPoll(
            slide, closed ? PollPhase.Closed : PollPhase.Open, round.Number, answered, counts, answerShown,
            answerShown ? settings.Correct : null, settings.Pick, settings.Max, settings.Answers, replies, words);
    }

    // PollReader.OpenOfAsync's rule: the replies in arrival order and, on a cloud, its words; nothing on a choice poll.
    private async Task<(IReadOnlyList<string> Replies, IReadOnlyList<PollWord> Words)> OpenOfAsync(PollAnswersForm form, Guid roundId, CancellationToken ct)
    {
        if (form == PollAnswersForm.Choice)
        {
            return ([], []);
        }
        var texts = await db.PollAnswers.AsNoTracking()
            .Where(a => a.RoundId == roundId && a.Text != null)
            .OrderBy(a => a.UpdatedAt)
            .Select(a => a.Text!)
            .ToListAsync(ct);
        return (OpenAnswers.Replies(texts), form == PollAnswersForm.Cloud ? OpenAnswers.Words(texts) : []);
    }
}
