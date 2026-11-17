using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls;

// The poll lifecycle (V4): open, close, reveal, show answer and new round on the deck's live audience session, and the
// presenter's view. Each action resolves the slide and runs every check inside one write transaction before it writes
// anything, so a refused action (ConflictException with a PollRefusals code, or 404 for a slide that is not a poll) changes
// nothing. A change records its activity entry in that transaction; after the commit, and never inside it, it calls
// IAudienceRelay.RefreshAsync and then IPollPublisher.PublishAsync, one after the other.
//
// No unique-index failure is caught: EF begins a SQLite transaction with BEGIN IMMEDIATE, which takes the write lock before
// the phase is read, so a second Open or New round waits, re-reads the rows under the lock and is refused (wrong_phase or
// another_poll_open) before it writes; the partial index can't be reached from one process.
//
// A draft row (OpenedAt null) exists only after New round on a manual poll. Opening or starting a round on one slide
// discards another slide's draft first (ClosedAt with no Final); its number stays used up. Close writes ClosedAt,
// JoinedAtClose and Final from the answer rows in one transaction, and the answer writer re-checks ClosedAt inside its own
// commit, so an answer is either in Final or refused. Ending the live session calls EndSessionAsync, which closes an open
// round the same way and discards a draft. Arrival is IPollArrival's: this class knows nothing of it.
//
// Per request.
[RegisterPerRequest(For = typeof(IPolls))]
public class Polls(
    AppDbContext db,
    PollReader reader,
    PollRows rows,
    IAudienceRoster roster,
    IActivityLog activity,
    IAudienceRelay relay,
    IPollPublisher publisher,
    ICurrentActor actor,
    TimeProvider clock) : IPolls
{
    public async Task<PollControlView> OpenAsync(string deckId, int slide)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var (poll, _) = await RequirePollAsync(deckId, slide);
            var session = await reader.LiveSessionAsync(deckId) ?? throw new ConflictException(PollRefusals.NOT_LIVE);
            var rounds = await reader.RoundsAsync(poll.Id, session, tracked: true);
            var draft = Phase(rounds) == PollPhase.Draft ? rounds.SingleOrDefault(r => r.ClosedAt == null) : throw new ConflictException(PollRefusals.WRONG_PHASE);
            var other = await OtherSlidesRoundAsync(session, poll.Id);

            var now = clock.GetUtcNow();
            await DiscardAsync(other, now);
            if (draft != null)
            {
                draft.OpenedAt = now;
            }
            else
            {
                db.PollRounds.Add(new PollRound { Id = Guid.NewGuid(), PollId = await rows.SlidePollIdAsync(poll.Id), Kind = PollKind.Slide, SlideId = poll.Id, SessionId = session, Number = PollReader.NextNumber(rounds), OpenedAt = now });
            }
            await db.SaveChangesAsync();
            await RecordAsync(deckId, $"opened poll on slide {slide}");
            await transaction.CommitAsync();
        }
        return await AnnounceAsync(deckId, slide);
    }

    public async Task<PollControlView> CloseAsync(string deckId, int slide)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var (poll, _) = await RequirePollAsync(deckId, slide);
            var round = await OpenRoundOnAsync(deckId, poll.Id) ?? throw new ConflictException(PollRefusals.WRONG_PHASE);
            var answered = await CloseRoundAsync(round, poll);
            await RecordAsync(deckId, ClosedSummary(slide, answered));
            await transaction.CommitAsync();
        }
        return await AnnounceAsync(deckId, slide);
    }

    public async Task<PollControlView> RevealAsync(string deckId, int slide)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var (poll, settings) = await RequirePollAsync(deckId, slide);
            var round = await OpenRoundOnAsync(deckId, poll.Id) ?? throw new ConflictException(PollRefusals.WRONG_PHASE);
            if (PollVisibility.RoomSees(settings.Results, round.RevealedAt != null, closed: false))
            {
                throw new ConflictException(PollRefusals.ALREADY_VISIBLE);
            }
            round.RevealedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            await RecordAsync(deckId, $"revealed results of poll on slide {slide}");
            await transaction.CommitAsync();
        }
        return await AnnounceAsync(deckId, slide);
    }

    // On a round whose answer is already shown, it succeeds and changes nothing: no write, no activity, no publish.
    public async Task<PollControlView> ShowAnswerAsync(string deckId, int slide)
    {
        bool shown;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var (poll, settings) = await RequirePollAsync(deckId, slide);
            var session = await reader.LiveSessionAsync(deckId);
            var rounds = session == null ? [] : await reader.RoundsAsync(poll.Id, session.Value, tracked: true);
            if (Phase(rounds) != PollPhase.Closed)
            {
                throw new ConflictException(PollRefusals.WRONG_PHASE);
            }
            if (settings.Correct == null)
            {
                throw new ConflictException(PollRefusals.NO_CORRECT);
            }
            var round = rounds.Last(PollReader.IsClosed);
            shown = round.AnswerShownAt != null;
            if (!shown)
            {
                round.AnswerShownAt = clock.GetUtcNow();
                await db.SaveChangesAsync();
                await RecordAsync(deckId, $"showed answer of poll on slide {slide}");
                await transaction.CommitAsync();
            }
        }
        return shown ? await ViewAsync(deckId, slide) : await AnnounceAsync(deckId, slide);
    }

    public async Task<PollControlView> NewRoundAsync(string deckId, int slide)
    {
        int number;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var (poll, settings) = await RequirePollAsync(deckId, slide);
            var session = await reader.LiveSessionAsync(deckId);
            var rounds = session == null ? [] : await reader.RoundsAsync(poll.Id, session.Value, tracked: true);
            if (session == null || Phase(rounds) != PollPhase.Closed)
            {
                throw new ConflictException(PollRefusals.WRONG_PHASE);
            }
            var other = await OtherSlidesRoundAsync(session.Value, poll.Id);

            var now = clock.GetUtcNow();
            await DiscardAsync(other, now);
            number = PollReader.NextNumber(rounds);
            var opened = settings.Opens == PollOpens.OnArrival ? now : (DateTimeOffset?)null;
            db.PollRounds.Add(new PollRound { Id = Guid.NewGuid(), PollId = await rows.SlidePollIdAsync(poll.Id), Kind = PollKind.Slide, SlideId = poll.Id, SessionId = session.Value, Number = number, OpenedAt = opened });
            await db.SaveChangesAsync();
            await RecordAsync(deckId, $"started round {number} of poll on slide {slide}");
            await transaction.CommitAsync();
        }
        return await AnnounceAsync(deckId, slide);
    }

    public Task<PollControlView?> ControlAsync(string deckId, int slide) => reader.ControlAsync(deckId, slide);

    // Ending the live session (LiveCommands.EndAsync, with the session it is ending, before it ends it): an open round is
    // closed with its Final, as Close does, and a draft is discarded. Does nothing when the session has no unclosed round. A
    // round on a slide that has since been deleted is closed but not published, since it has no slide number. It refreshes
    // and publishes but reads no view, since the caller needs none.
    public async Task EndSessionAsync(string deckId, Guid sessionId)
    {
        int? slide;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            if (await rows.OpenRoundAsync(sessionId) is not { } round)
            {
                return;
            }
            var poll = await db.Slides.FindAsync(round.SlideId) ?? throw new InvalidOperationException($"Poll round {round.Id} has no slide.");
            slide = poll.DeletedAt == null ? poll.Position + 1 : null;
            if (round.OpenedAt == null)
            {
                await DiscardAsync(round, clock.GetUtcNow());
            }
            else
            {
                var answered = await CloseRoundAsync(round, poll);
                await RecordAsync(deckId, slide == null ? $"closed poll on a deleted slide with {Answers(answered)}" : ClosedSummary(slide.Value, answered));
            }
            await transaction.CommitAsync();
        }
        if (slide != null)
        {
            await relay.RefreshAsync(deckId);
            await publisher.PublishAsync(deckId, slide.Value);
        }
    }

    // The slide's phase in the session from its rounds: the unclosed round's (draft or open), else closed when a round was
    // opened, else draft. A discarded draft is not a phase of its own.
    private static PollPhase Phase(IReadOnlyList<PollRound> rounds)
    {
        if (rounds.SingleOrDefault(r => r.ClosedAt == null) is { } unclosed)
        {
            return unclosed.OpenedAt == null ? PollPhase.Draft : PollPhase.Open;
        }
        return rounds.Any(PollReader.IsClosed) ? PollPhase.Closed : PollPhase.Draft;
    }

    private static string ClosedSummary(int slide, int answered) => $"closed poll on slide {slide} with {Answers(answered)}";

    private static string Answers(int count) => count == 1 ? "1 answer" : $"{count} answers";

    // The slide at that number, or 404 when it is not a poll (PollAccess.NOT_A_POLL, as a GET of it gives).
    private async Task<(Slide Slide, PollSettings Settings)> RequirePollAsync(string deckId, int slide)
        => await reader.SlideAsync(deckId, slide) ?? throw new NotFoundException(PollAccess.NOT_A_POLL);

    // The live session's open round when it is on this slide, tracked, else null (not live, draft, closed, or another slide's).
    private async Task<PollRound?> OpenRoundOnAsync(string deckId, Guid slideId)
        => await reader.LiveSessionAsync(deckId) is { } session && await rows.OpenRoundAsync(session) is { OpenedAt: not null } round && round.SlideId == slideId
            ? round
            : null;

    // The session's unclosed round on another slide: a draft to discard, or null. A round that is really open there refuses.
    private async Task<PollRound?> OtherSlidesRoundAsync(Guid session, Guid slideId)
    {
        var other = await rows.OpenRoundAsync(session);
        if (other == null || other.SlideId == slideId)
        {
            return null;
        }
        return other.OpenedAt == null ? other : throw new ConflictException(PollRefusals.ANOTHER_POLL_OPEN);
    }

    // Ends a draft with no Final and no JoinedAtClose. Saved here, before a caller adds the session's next unclosed round,
    // because the partial unique index allows one.
    private async Task DiscardAsync(PollRound? draft, DateTimeOffset now)
    {
        if (draft == null)
        {
            return;
        }
        draft.ClosedAt = now;
        await db.SaveChangesAsync();
    }

    // Sets ClosedAt, JoinedAtClose (the roster's count) and Final, counted from the answer rows with the slide's option
    // texts now. Runs inside the caller's transaction. Returns how many answered.
    private async Task<int> CloseRoundAsync(PollRound round, Slide poll)
    {
        var (_, options) = PollReader.SlotsOf(poll.SlotsJson);
        var answers = await db.PollAnswers.Where(a => a.RoundId == round.Id).Select(a => a.Picks).ToListAsync();
        var counts = options.Select((_, position) => answers.Count(picks => picks.Contains(position))).ToList();
        round.ClosedAt = clock.GetUtcNow();
        round.JoinedAtClose = await roster.CountAsync(round.SessionId.ToString());
        round.Final = new PollFinal(options, counts, answers.Count);
        await db.SaveChangesAsync();
        return answers.Count;
    }

    private Task RecordAsync(string deckId, string summary) => activity.RecordAsync(deckId, ActivityEntry.By(actor, tool: null, summary));

    // After the commit: the audience state, then the poll events, one after the other, then the caller's view.
    private async Task<PollControlView> AnnounceAsync(string deckId, int slide)
    {
        await relay.RefreshAsync(deckId);
        await publisher.PublishAsync(deckId, slide);
        return await ViewAsync(deckId, slide);
    }

    private async Task<PollControlView> ViewAsync(string deckId, int slide)
        => await reader.ControlAsync(deckId, slide) ?? throw new NotFoundException(PollAccess.NOT_A_POLL);
}
