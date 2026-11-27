using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.OpenResponses;
using SlideWorks.Core.Slides.Layouts;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.OpenResponses.Data;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// A word-cloud slide's round in the deck's live session, read from its rows, as the presenter's event: every word, a hidden
// one with Hidden set. The current round is the session's unclosed round once opened (open), else the last opened round
// (closed), else none (draft). A word is its text key (D2): its count is the guests holding it in rows not hidden, and a
// word whose every row is hidden is Hidden and counts those rows' guests. Guests counts guests with a word not hidden.
// Words go by count descending, then by key (D11). Results are live and a round opens showing its words (D10, D14), so
// Revealed is always true. Null when the slide is not a live word cloud with an open-response row.
[RegisterPerRequest]
public class WordCloudReader(AppDbContext db, IAudienceSessions sessions)
{
    public async Task<WordCloudTally?> ReadAsync(string deckId, int slide, CancellationToken cancellationToken)
    {
        var slideId = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null && s.Position == slide - 1 && s.Layout == WordCloudLayout.Definition.Name)
            .Select(s => (Guid?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (slideId is null || await sessions.ForDeckAsync(deckId) is not { Phase: AudiencePhase.Live, SessionId: { } session })
        {
            return null;
        }
        var responseId = await db.OpenResponses.AsNoTracking()
            .Where(r => r.SlideId == slideId && r.Kind == OpenResponseKind.WordCloud)
            .Select(r => (Guid?)r.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (responseId is null)
        {
            return null;
        }
        var sessionId = Guid.Parse(session);
        var rounds = await db.OpenRounds.AsNoTracking()
            .Where(r => r.OpenResponseId == responseId && r.SessionId == sessionId)
            .OrderBy(r => r.Number)
            .ToListAsync(cancellationToken);
        var unclosed = rounds.SingleOrDefault(r => r.ClosedAt == null);
        var current = unclosed switch
        {
            { OpenedAt: not null } => unclosed,
            null => rounds.LastOrDefault(r => r.OpenedAt != null),
            _ => null,
        };
        if (current == null)
        {
            var number = unclosed?.Number ?? (rounds.Count == 0 ? 1 : rounds[^1].Number + 1);
            return new WordCloudTally(deckId, OpenResponseTarget.Presenter, slide, OpenResponsePhase.Draft, null, number, unclosed?.Revision ?? 0, true, 0, []);
        }
        var entries = await db.OpenEntries.AsNoTracking().Where(e => e.RoundId == current.Id).ToListAsync(cancellationToken);
        var phase = current.ClosedAt == null ? OpenResponsePhase.Open : OpenResponsePhase.Closed;
        var guests = WordCloudCounts.GuestsOf(entries);
        var words = WordCloudCounts.WordsOf(entries);
        return new WordCloudTally(deckId, OpenResponseTarget.Presenter, slide, phase, current.Id.ToString(), current.Number, current.Revision, true, guests, words);
    }
}
