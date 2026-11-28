using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Polls;

// The only place a PollActivity is built (V3, V5): from the deck's live audience session, the presenter's slide in SW-4's
// presentation state (past the end of the deck, the last slide, as the feed shows it), and the poll's rounds on that slide
// in the session. Null outside phase live, with no presentation, and on a slide that is not a poll. The phase follows the presenter's view (PollReader): the unclosed round's (draft or
// open), else closed when a round was opened, else draft. In draft, Round is null and Number is the number the round will
// get. A closed round shows the option texts from its Final. Correct is the setting only once the answer is shown.
// RoomSeesCounts is PollVisibility's. It never carries counts, Opens or Privacy.
//
// The feed calls it under its per-deck gate on every compose, so it only reads, with indexed reads, and never publishes.
// Per request, because it uses the database.
[RegisterPerRequest(For = typeof(IAudienceActivity))]
public class AudienceActivity(AppDbContext db, IAudienceSessions sessions, IPresentations presentations, PollReader reader) : IAudienceActivity
{
    // TODO: the language is ignored here and in ForCodeAsync, so every activity is the primary one; translating the
    // activity replaces this.
    public async Task<PollActivity?> ForDeckAsync(string deckId, string? language)
        => await sessions.ForDeckAsync(deckId) is { Phase: AudiencePhase.Live, SessionId: { } session }
            ? await BuildAsync(deckId, Guid.Parse(session))
            : null;

    public async Task<PollActivity?> ForCodeAsync(string code, string? language)
        => await sessions.FindAsync(code) is { Phase: AudiencePhase.Live, SessionId: { } session } found
            ? await BuildAsync(found.DeckId, Guid.Parse(session))
            : null;

    private async Task<PollActivity?> BuildAsync(string deckId, Guid session)
    {
        if (await presentations.GetAsync(deckId) is not { } presentation || await ShownAsync(deckId, presentation.Slide) is not { } shown
            || await reader.SlideAsync(deckId, shown) is not { } found)
        {
            return null;
        }
        var (slide, settings) = found;
        var (question, options) = PollReader.SlotsOf(slide.SlotsJson);
        var rounds = await reader.RoundsAsync(slide.Id, session, tracked: false);

        if (rounds.SingleOrDefault(r => r.ClosedAt == null) is { } unclosed)
        {
            return unclosed.OpenedAt == null
                ? Draft(unclosed.Number, question, options, settings)
                : Activity(unclosed, PollPhase.Open, question, options, settings, correct: null);
        }
        if (rounds.LastOrDefault(PollReader.IsClosed) is { } last)
        {
            var correct = last.AnswerShownAt == null ? null : settings.Correct;
            return Activity(last, PollPhase.Closed, question, last.Final!.Options, settings, correct);
        }
        return Draft(PollReader.NextNumber(rounds), question, options, settings);
    }

    // The slide the guests see for the presenter's position: past the end of the deck (slides removed while live) it is the
    // last slide. This mirrors AudienceFeed.Project's rule (Realtime/AudienceFeed.cs, "Math.Clamp(presentation.Slide, 1,
    // slides.Count)"), which is private there, so the card always matches the slide on the phone. Null for an empty deck.
    private async Task<int?> ShownAsync(string deckId, int slide)
    {
        var count = await db.Slides.AsNoTracking().CountAsync(s => s.DeckId == deckId && s.DeletedAt == null);
        return count == 0 ? null : Math.Clamp(slide, 1, count);
    }

    private static PollActivity Draft(int number, string question, IReadOnlyList<string> options, PollSettings settings)
        => new(null, number, PollPhase.Draft, question, options, settings.Pick, settings.Max, settings.Results,
            PollVisibility.RoomSees(settings.Results, revealed: false, closed: false), null, settings.Answers);

    private static PollActivity Activity(PollRound round, PollPhase phase, string question, IReadOnlyList<string> options, PollSettings settings, int? correct)
        => new(round.Id.ToString(), round.Number, phase, question, options, settings.Pick, settings.Max, settings.Results,
            PollVisibility.RoomSees(settings.Results, round.RevealedAt != null, closed: phase == PollPhase.Closed), correct, settings.Answers);
}
