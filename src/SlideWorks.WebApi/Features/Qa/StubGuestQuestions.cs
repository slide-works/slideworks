using Agoda.IoC.Core;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// TODO: replace with the real guest view and votes: the roster, the room, the stored questions, the tally and the answer writer.
// Answers every guest with guest-questions.json's "view" sample and accepts every vote with the next revision.
[RegisterPerRequest(For = typeof(IGuestQuestions))]
public sealed class StubGuestQuestions : IGuestQuestions
{
    private const string ROUND = "4f1d2c8e-9a7b-4e3f-8c21-6b5a0d9e7f12";

    private static readonly GuestQuestionsView _view = new(
        new QaRoom(ROUND, QaRoomQuestions.Open, true, true, 57),
        [
            new GuestQuestion(1, "How do you split a big change?", "Priya", "12 min", QuestionStatus.Open, 14, false, false),
            new GuestQuestion(2, "What is the smallest slice you have shipped?", null, "9 min", QuestionStatus.Open, 9, true, false),
            new GuestQuestion(3, "Do feature flags count as shipping?", "Sam K", "8 min", QuestionStatus.Answered, 6, false, false),
            new GuestQuestion(4, "Who reviews the slices?", null, "2 min", QuestionStatus.Waiting, 1, false, true),
            new GuestQuestion(6, "How do you handle database migrations?", null, "now", QuestionStatus.Open, 3, false, false),
        ],
        new MyVotes([1, 4], 3));

    public Task<VoteResult> VoteAsync(string code, string guestId, VoteRequest request)
        => Task.FromResult<VoteResult>(new VoteResult.Voted(request.Picks, request.Revision + 1));

    public Task<GuestQuestionsView?> ViewAsync(string code, string guestId) => Task.FromResult<GuestQuestionsView?>(_view);
}
