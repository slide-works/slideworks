using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Features.Render.Print;
using SlideWorks.Core.OpenResponses;
using SlideWorks.Core.Slides.Layouts;

namespace SlideWorks.WebApi.Features.OpenResponses;

// The word-cloud and open-text results of one session (live or ended) for the print view: for each live slide of the deck
// with an opened round in the session, its newest opened round as the record the big screen gets, with hidden words and
// answers left out. Words and answer numbers follow the same rules as the big screen (WordCloudCounts, OpenTextWall); an
// answer keeps its number, so hiding one leaves a gap. Guests is what the big screen gets and counts hidden answers too.
[RegisterPerRequest(For = typeof(IPrintOpenResponseResults))]
public class PrintOpenResponseResults(AppDbContext db) : IPrintOpenResponseResults
{
    public async Task<PrintOpenResponses> ReadAsync(string deckId, Guid sessionId, CancellationToken ct)
    {
        var rounds = await (
                from slide in db.Slides.AsNoTracking()
                join response in db.OpenResponses.AsNoTracking() on slide.Id equals response.SlideId
                join round in db.OpenRounds.AsNoTracking() on response.Id equals round.OpenResponseId
                where slide.DeckId == deckId && slide.DeletedAt == null && round.SessionId == sessionId && round.OpenedAt != null
                orderby slide.Position, round.Number descending
                select new { slide.Position, slide.Layout, response.Kind, Round = round })
            .ToListAsync(ct);
        var newest = rounds
            .Where(r => r.Kind == OpenResponseKind.WordCloud ? r.Layout == WordCloudLayout.Definition.Name : r.Layout == OpenTextLayout.Definition.Name)
            .GroupBy(r => (r.Position, r.Kind)).Select(g => g.First()).OrderBy(r => r.Position).ToList();
        var wordClouds = new List<WordCloudTally>();
        var openTexts = new List<OpenTextTally>();
        foreach (var found in newest)
        {
            var round = found.Round;
            var phase = round.ClosedAt == null ? OpenResponsePhase.Open : OpenResponsePhase.Closed;
            if (found.Kind == OpenResponseKind.WordCloud)
            {
                var entries = await db.OpenEntries.AsNoTracking().Where(e => e.RoundId == round.Id).ToListAsync(ct);
                IReadOnlyList<CloudWord> words = [.. WordCloudCounts.WordsOf(entries).Where(w => !w.Hidden)];
                wordClouds.Add(new WordCloudTally(deckId, OpenResponseTarget.Audience, found.Position + 1, phase, round.Id.ToString(), round.Number, round.Revision, true, WordCloudCounts.GuestsOf(entries), words));
            }
            else
            {
                var entries = await OpenTextWall.InArrivalOrder(db.OpenEntries.AsNoTracking(), round.Id).ToListAsync(ct);
                IReadOnlyList<WallAnswer> answers = [.. OpenTextWall.Number(entries.Select(e => (e.Text, e.HiddenAt != null))).Where(a => !a.Hidden)];
                openTexts.Add(new OpenTextTally(deckId, OpenResponseTarget.Audience, found.Position + 1, phase, round.Id.ToString(), round.Number, round.Revision, true, entries.Count, answers));
            }
        }
        return new PrintOpenResponses(wordClouds, openTexts);
    }
}
