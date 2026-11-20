using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Realtime;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Features.Qa;

// The presenter's view of a deck's Q&A (Q6), read from the rows of the deck's live audience session. Not live: Live false,
// no round, no here, no questions, zero counts and zero reactions, with the settings and the switched-off slides. Live: every
// question of the session's open Q&A round in number order, its text from the round's PollOption rows and its votes from
// IPollTallies (sized by the round's option count, so a question added since the last vote reads zero); Here from SW-4's
// presentation state (null when the registry holds none for the deck) and that slide's switch; the session's reaction
// totals from IReactions. Questions off reads as no open round (D3): round null, no questions, zero counts and nothing on
// screen; the round and its questions stay stored and come back when questions leave off. Seq comes from QaSeq and is taken
// before any read, so a view stamped later never holds older rows. The questions are read before their options: a question
// and its option commit together, so every question read has its option, and a question asked between the two reads only
// adds an option nobody lists. Per request, because it uses the database.
[RegisterPerRequest]
public class QaReader(
    AppDbContext db,
    QaRows rows,
    IAudienceSessions sessions,
    IPresentations presentations,
    IQaSettings settings,
    IPollTallies tallies,
    IReactions reactions,
    QaSeq seq,
    TimeProvider clock)
{
    public async Task<QaControlView> ControlAsync(string deckId)
    {
        var stamp = seq.Next();
        var current = await settings.GetAsync(deckId);
        var slides = (await rows.SwitchesAsync(deckId))
            .OrderBy(s => s.Key)
            .Select(s => new QaSlideView(s.Key, s.Value.Questions, s.Value.Reactions))
            .ToList();
        if (await LiveSessionAsync(deckId) is not { } session)
        {
            return new QaControlView(stamp, false, null, current, null, slides, new QaCounts(0, 0, 0, 0), null, [], ReactionCounts.Zero);
        }
        var round = current.Questions == QaQuestions.Off ? null : await OpenRoundAsync(session);
        var questions = round is { } id ? await QuestionsAsync(id) : [];
        return new QaControlView(
            stamp,
            true,
            round?.ToString(),
            current,
            await HereAsync(deckId),
            slides,
            CountsOf(questions),
            questions.SingleOrDefault(q => q.OnScreen)?.Number,
            questions,
            await reactions.TotalsAsync(session));
    }

    // The deck's live audience session, or null when it isn't live. The phase rule is IAudienceSessions'.
    public async Task<Guid?> LiveSessionAsync(string deckId)
        => await sessions.ForDeckAsync(deckId) is { Phase: AudiencePhase.Live, SessionId: { } id } ? Guid.Parse(id) : null;

    // The session's open Q&A round as the presenter acts on it: null when the deck's questions are off, or when the session
    // has no open Q&A round (questions were off at Go live).
    public async Task<Guid?> ActiveRoundAsync(string deckId, Guid sessionId)
        => (await settings.GetAsync(deckId)).Questions == QaQuestions.Off ? null : await OpenRoundAsync(sessionId);

    // The session's open Q&A round, or null when it has none (questions were off at Go live).
    public async Task<Guid?> OpenRoundAsync(Guid sessionId)
        => await db.PollRounds.AsNoTracking()
            .Where(r => r.SessionId == sessionId && r.Kind == PollKind.Qa && r.ClosedAt == null)
            .Select(r => (Guid?)r.Id)
            .SingleOrDefaultAsync();

    private async Task<IReadOnlyList<QaQuestion>> QuestionsAsync(Guid roundId)
    {
        var questions = await rows.QuestionsAsync(roundId);
        var texts = await db.PollOptions.AsNoTracking()
            .Where(o => o.RoundId == roundId)
            .ToDictionaryAsync(o => o.Position, o => o.Text);
        var tally = await tallies.ReadAsync(roundId.ToString(), texts.Count);
        var now = clock.GetUtcNow();
        return questions
            .Select(q => new QaQuestion(
                q.Position + 1,
                texts[q.Position],
                q.Name,
                QuestionAge.Label(q.AskedAt, now),
                q.State,
                q.HiddenAt != null,
                q.OnScreen,
                tally.Counts[q.Position]))
            .ToList();
    }

    // The presenter's slide and its switch. A position past the end of the deck (slides removed while live) is the last
    // slide, as the audience feed shows it. Null when SW-4's registry holds no state for the deck or the deck has no slides.
    private async Task<QaSlideView?> HereAsync(string deckId)
    {
        if (await presentations.GetAsync(deckId) is not { } presentation)
        {
            return null;
        }
        var ids = await db.Slides.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.DeletedAt == null)
            .OrderBy(s => s.Position)
            .Select(s => s.Id)
            .ToListAsync();
        if (ids.Count == 0)
        {
            return null;
        }
        var slide = Math.Clamp(presentation.Slide, 1, ids.Count);
        var slideSwitch = await rows.SwitchAsync(ids[slide - 1]);
        return new QaSlideView(slide, slideSwitch.Questions, slideSwitch.Reactions);
    }

    // A hidden question counts only as hidden; every other one by its state.
    private static QaCounts CountsOf(IReadOnlyList<QaQuestion> questions)
    {
        var shown = questions.Where(q => !q.Hidden).ToList();
        return new QaCounts(
            shown.Count(q => q.State == QuestionState.Open),
            shown.Count(q => q.State == QuestionState.Waiting),
            shown.Count(q => q.State == QuestionState.Answered),
            questions.Count - shown.Count);
    }
}
