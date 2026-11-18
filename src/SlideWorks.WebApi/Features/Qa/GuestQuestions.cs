using Agoda.IoC.Core;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Features.Qa.Data;

namespace SlideWorks.WebApi.Features.Qa;

// A guest's view of the Q&A round and its upvotes (Q4). Every check of a vote runs before anything is written, in this order:
// no live session for the code, or a guest not on its roster, is NoSession, so a guessed code or guest id learns nothing; no
// round (questions off) is questions_off; then the guest's 30 vote changes a minute (VoteLimiter); then stale (not the
// room's round, or not the guest's stored revision); then the picks' rules (VoteRules). A vote that passes stores the
// guest's old picks on questions that can't take a vote now plus the asked picks on ones that can, through the batching
// writer (IRoundAnswerWriter), which checks the round and the revision again inside its commit: a vote it refuses comes back
// stale with the guest's current view. Upvotes count while questions are closed or off on the slide on screen.
[RegisterPerRequest(For = typeof(IGuestQuestions))]
public class GuestQuestions(
    GuestPollViews seats,
    IAudienceRoom rooms,
    GuestQuestionViews views,
    QaRows rows,
    VoteLimiter limiter,
    IRoundAnswerWriter writer) : IGuestQuestions
{
    public async Task<VoteResult> VoteAsync(string code, string guestId, VoteRequest request)
    {
        if (await seats.SeatAsync(code, guestId) is not { SessionId: { } sessionId } seat
            || await rooms.ForDeckAsync(seat.DeckId) is not { } room)
        {
            return new VoteResult.NoSession();
        }
        if (!Guid.TryParse(room.Round, out var roundId))
        {
            return new VoteResult.Refused(GuestQaErrors.QUESTIONS_OFF);
        }
        if (!limiter.TryAcquire(guestId))
        {
            return new VoteResult.Limited();
        }
        var answer = await views.AnswerAsync(roundId, guestId);
        if (!Guid.TryParse(request.Round, out var asked) || asked != roundId || (answer?.Revision ?? 0) != request.Revision)
        {
            return await StaleAsync(seat.DeckId, guestId);
        }
        var questions = await rows.QuestionsAsync(roundId);
        if (VoteRules.Check(request.Picks, questions) is { } broken)
        {
            return new VoteResult.Invalid(broken);
        }

        var picks = VoteRules.Stored(answer?.Picks ?? [], request.Picks, questions);
        var target = new PollRoundRef(seat.DeckId, PollKind.Qa, null, null, sessionId.ToString(), roundId.ToString(), await views.OptionCountAsync(roundId));
        return await writer.ReplaceAsync(target, guestId, picks, request.Revision) is RoundWrite.Written written
            ? new VoteResult.Voted([.. written.Picks.Select(position => position + 1)], written.Revision)
            : await StaleAsync(seat.DeckId, guestId);
    }

    public async Task<GuestQuestionsView?> ViewAsync(string code, string guestId)
        => await seats.SeatAsync(code, guestId) is { SessionId: not null } seat ? await views.ViewAsync(seat.DeckId, guestId) : null;

    // The guest's view now, for a stale vote; NoSession when the session ended meanwhile.
    private async Task<VoteResult> StaleAsync(string deckId, string guestId)
        => await views.ViewAsync(deckId, guestId) is { } current ? new VoteResult.Stale(current) : new VoteResult.NoSession();
}
