using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// The one way a guest's answer in a round is written (SW-9 Q3), for every kind of round: a slide poll's answer from
// IPollAnswers and a guest's upvotes in a Q&A round from SW-9's asking and voting seams. Behind it is SW-6's batching
// AnswerWriter (D16): the answer is queued, committed with whatever else is queued, and only then applied to the tally, so
// no caller opens a transaction per answer. Inside the commit the round's ClosedAt and the guest's revision are checked
// again. Which picks are allowed (the poll's rules, which questions can take a vote) is the caller's to check first; the
// writer stores the picks it is given. A singleton.
public interface IRoundAnswerWriter
{
    // Replaces the guest's answer in the round with picks (0-based option positions) when the guest's stored revision is
    // still expectedRevision (0 for no answer yet), and completes once its group has committed. Throws what the group's
    // commit threw when it failed; nothing of that group is written or counted.
    Task<RoundWrite> ReplaceAsync(PollRoundRef round, string guestId, IReadOnlyList<int> picks, int expectedRevision, string? text = null);
}
