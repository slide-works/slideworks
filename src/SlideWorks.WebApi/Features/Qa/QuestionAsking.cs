using Agoda.IoC.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Qa.Data;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Qa;

// A guest asking a question (Q4). Every check runs before any write, in this order: a code with no live session, or a guest
// not on its roster, is NoSession; then the room on screen (IAudienceRoom: off, closed and offHere are refused with their
// codes); then the guest's 3 a minute (AskLimiter); then the text and the name (QuestionText, with the room's names setting).
// The limit is taken before the text is checked and given back when the ask is refused after it, so a refused ask doesn't use
// it up. Then one transaction writes the question's PollOption (its text) and its Question at the round's next position,
// waiting when the deck's review setting is on; two asks at once that pick the same position clash on the key, and the loser
// tries again at the next one. Then the asker's upvote goes through IRoundAnswerWriter with the guest's other upvotes kept;
// it doesn't count against the upvote limit, and when the writer refuses it (Stale, Closed) or its commit fails, the question
// stands without it. Then the round's list revision moves and the publisher runs; a failed publish is logged and doesn't
// fail the ask, since the question is stored. No log line carries the question's text or the guest's id.
[RegisterPerRequest(For = typeof(IQuestionAsking))]
public class QuestionAsking(
    AppDbContext db,
    IAudienceSessions sessions,
    IAudienceRoom rooms,
    QaRows qaRows,
    AskLimiter limiter,
    IRoundAnswerWriter writer,
    QaRevisions revisions,
    IQuestionPublisher publisher,
    TimeProvider clock,
    ILogger<QuestionAsking> log) : IQuestionAsking
{
    // SQLITE_CONSTRAINT_PRIMARYKEY: the extended code of a clash on (RoundId, Position).
    private const int SQLITE_CONSTRAINT_PRIMARYKEY = 1555;

    // Each clash means another ask took the position, so the next attempt reads a higher one.
    private const int STORE_ATTEMPTS = 5;

    public async Task<AskResult> AskAsync(string code, string guestId, AskRequest request)
    {
        if (await SessionAsync(code, guestId) is not { } session)
        {
            return new AskResult.NoSession();
        }
        var room = await rooms.ForDeckAsync(session.DeckId);
        if (room == null)
        {
            return new AskResult.NoSession();
        }
        if (RefusalOf(room.Questions) is { } refusal)
        {
            return new AskResult.Refused(refusal);
        }
        if (limiter.TryTake(guestId, out var retryAfter) is not { } taken)
        {
            return new AskResult.Limited(retryAfter);
        }

        Stored? stored = null;
        try
        {
            if (QuestionText.Check(request.Text, request.Name, room.Names) is { } broken)
            {
                return new AskResult.Invalid(broken);
            }
            var review = (await qaRows.SettingsAsync(session.DeckId)).Review;
            stored = await StoreAsync(session.Id, guestId, QuestionText.Text(request.Text), QuestionText.Name(request.Name), review);
            if (stored == null)
            {
                return new AskResult.Refused(GuestQaErrors.QUESTIONS_OFF);
            }
        }
        finally
        {
            if (stored == null)
            {
                limiter.GiveBack(guestId, taken);
            }
        }

        var round = stored.Question.RoundId.ToString();
        var mine = await UpvoteAsync(new PollRoundRef(session.DeckId, PollKind.Qa, null, null, session.Id.ToString(), round, stored.Question.Position + 1), guestId, stored.Question.Position);
        revisions.Bump(round);
        await PublishAsync(session.DeckId);

        var question = stored.Question;
        var state = question.State == QuestionState.Waiting ? QuestionStatus.Waiting : QuestionStatus.Open;
        var votes = mine.Picks.Contains(question.Position + 1) ? 1 : 0;
        return new AskResult.Asked(
            new GuestQuestion(question.Position + 1, stored.Text, question.Name, QuestionAge.Label(question.AskedAt, clock.GetUtcNow()), state, votes, false, true),
            mine);
    }

    // The code's live session when this guest is on its roster, else null.
    private async Task<(string DeckId, Guid Id)?> SessionAsync(string code, string guestId)
    {
        if (await sessions.FindAsync(code) is not { Phase: AudiencePhase.Live, SessionId: { } id } found)
        {
            return null;
        }
        var sessionId = Guid.Parse(id);
        return await db.AudienceJoins.AsNoTracking().AnyAsync(j => j.SessionId == sessionId && j.GuestId == guestId)
            ? (found.DeckId, sessionId)
            : null;
    }

    private static string? RefusalOf(QaRoomQuestions questions) => questions switch
    {
        QaRoomQuestions.Off => GuestQaErrors.QUESTIONS_OFF,
        QaRoomQuestions.Closed => GuestQaErrors.QUESTIONS_CLOSED,
        QaRoomQuestions.OffHere => GuestQaErrors.QUESTIONS_OFF_HERE,
        _ => null,
    };

    // Writes the option and the question at the session's open Q&A round's next position, in one transaction, or returns
    // null when the session has no open Q&A round.
    private async Task<Stored?> StoreAsync(Guid sessionId, string guestId, string text, string? name, bool review)
    {
        for (var attempt = 1; attempt <= STORE_ATTEMPTS; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var roundId = await db.PollRounds.AsNoTracking()
                .Where(r => r.SessionId == sessionId && r.Kind == PollKind.Qa && r.ClosedAt == null && r.OpenedAt != null)
                .Select(r => (Guid?)r.Id)
                .SingleOrDefaultAsync();
            if (roundId is not { } id)
            {
                return null;
            }
            var position = (await db.PollOptions.AsNoTracking().Where(o => o.RoundId == id).MaxAsync(o => (int?)o.Position) ?? -1) + 1;
            var now = clock.GetUtcNow();
            var option = new PollOption { RoundId = id, Position = position, Text = text, CreatedAt = now };
            var question = new Question
            {
                RoundId = id,
                Position = position,
                GuestId = guestId,
                Name = name,
                State = review ? QuestionState.Waiting : QuestionState.Open,
                AskedAt = now,
            };
            db.PollOptions.Add(option);
            db.Questions.Add(question);
            try
            {
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return new Stored(text, question);
            }
            catch (DbUpdateException ex) when (attempt < STORE_ATTEMPTS && IsPositionClash(ex))
            {
                db.Entry(option).State = EntityState.Detached;
                db.Entry(question).State = EntityState.Detached;
            }
        }
        throw new InvalidOperationException($"No position was free in {STORE_ATTEMPTS} attempts.");
    }

    // Adds the question to the guest's upvotes, keeping the others, and returns what is stored afterwards.
    private async Task<MyVotes> UpvoteAsync(PollRoundRef round, string guestId, int position)
    {
        var roundId = Guid.Parse(round.RoundId);
        var before = await MineAsync(roundId, guestId);
        try
        {
            var picks = before.Picks.Append(position).Distinct().Order().ToList();
            if (await writer.ReplaceAsync(round, guestId, picks, before.Revision) is RoundWrite.Written written)
            {
                return new MyVotes([.. written.Picks.Select(p => p + 1)], written.Revision);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "The asker's upvote in Q&A round {RoundId} could not be written; the question stands without it", round.RoundId);
        }
        var now = await MineAsync(roundId, guestId);
        return new MyVotes([.. now.Picks.Select(p => p + 1)], now.Revision);
    }

    // The guest's stored upvotes in the round as 0-based positions, ascending, and their revision (0 before the first).
    private async Task<(IReadOnlyList<int> Picks, int Revision)> MineAsync(Guid roundId, string guestId)
    {
        var answer = await db.PollAnswers.AsNoTracking()
            .Where(a => a.RoundId == roundId && a.GuestId == guestId)
            .Select(a => new { a.Picks, a.Revision })
            .SingleOrDefaultAsync();
        return answer == null ? ([], 0) : ([.. answer.Picks.Order()], answer.Revision);
    }

    private async Task PublishAsync(string deckId)
    {
        try
        {
            await publisher.PublishAsync(deckId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Publishing the questions of deck {DeckId} after an ask failed", deckId);
        }
    }

    private static bool IsPositionClash(DbUpdateException ex)
        => ex.InnerException is SqliteException { SqliteExtendedErrorCode: SQLITE_CONSTRAINT_PRIMARYKEY };

    // The question as stored, with its cleaned text (held by its PollOption).
    private sealed record Stored(string Text, Question Question);
}
