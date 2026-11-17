using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// A guest's answers (V5). Code is the audience session's join code and guestId the sw_guest cookie's value. Per request,
// because it uses the database.
public interface IPollAnswers
{
    // Validates and queues the answer for the batching writer, and returns once its group has committed (D16).
    Task<AnswerResult> SubmitAsync(string code, string guestId, AnswerRequest request);

    // What the phone shows: the activity (from IAudienceActivity.ForCodeAsync), this guest's own answer and the tally as
    // PollVisibility lets this guest see it. Null for an unknown code.
    Task<GuestPollView?> ViewAsync(string code, string guestId);

    // This guest's own answer in the round, or null when it has none.
    Task<MyAnswer?> MineAsync(string roundId, string guestId);

    // The open round's id when the poll's results show after answering and this guest has answered in it, so it may join
    // poll-results:{roundId}; null otherwise.
    Task<string?> CanWatchAsync(string code, string guestId);
}
