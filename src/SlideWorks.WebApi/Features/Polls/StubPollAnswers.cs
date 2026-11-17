using Agoda.IoC.Core;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// TODO: replace with the real answers: validation, the batching writer, the guest's view and the results-group check.
// Accepts every answer and echoes its picks with the next revision; the view is the "hidden" sample of guest-poll.json.
[RegisterPerRequest(For = typeof(IPollAnswers))]
public sealed class StubPollAnswers : IPollAnswers
{
    private const string ROUND = "0b7c6a52-1f0e-4f43-9d3a-5a1c2e8b7d10";

    private static readonly GuestPollView _view = new(
        new PollActivity(
            ROUND,
            1,
            PollPhase.Open,
            "How big was your last merge request?",
            ["Under 50 lines", "50 to 200 lines", "200 to 1,000 lines", "Over 1,000 lines"],
            PollPick.One,
            null,
            PollResults.AfterClose,
            false,
            null),
        new MyAnswer([2], 1),
        new PollTally(string.Empty, PollTallyTarget.Audience, ROUND, 21, null),
        false);

    public Task<AnswerResult> SubmitAsync(string code, string guestId, AnswerRequest request)
        => Task.FromResult<AnswerResult>(new AnswerResult.Accepted(request.Picks, request.Revision + 1));

    public Task<GuestPollView?> ViewAsync(string code, string guestId) => Task.FromResult<GuestPollView?>(_view);

    public Task<MyAnswer?> MineAsync(string roundId, string guestId) => Task.FromResult<MyAnswer?>(null);

    public Task<string?> CanWatchAsync(string code, string guestId) => Task.FromResult<string?>(null);
}
