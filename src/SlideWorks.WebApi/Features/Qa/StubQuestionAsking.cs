using Agoda.IoC.Core;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// TODO: replace with the real asking: the roster, the room, the limiter, the text rules, the stored question and the asker's upvote.
// Accepts every question and echoes it as question 6, open, with the asker's upvote, the shape of question-ask.json's "created".
[RegisterPerRequest(For = typeof(IQuestionAsking))]
public sealed class StubQuestionAsking : IQuestionAsking
{
    public Task<AskResult> AskAsync(string code, string guestId, AskRequest request)
        => Task.FromResult<AskResult>(new AskResult.Asked(
            new GuestQuestion(6, QuestionText.Text(request.Text), QuestionText.Name(request.Name), "now", QuestionStatus.Open, 1, false, true),
            new MyVotes([1, 6], 4)));
}
