using Agoda.IoC.Core;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// TODO: replace with the real moderation: the state rules, the stored questions, the tally, the switches and the reaction totals.
// Answers every deck with qa-control.json's "live" sample, and every action returns it unchanged.
[RegisterPerRequest(For = typeof(IQuestionModeration))]
public sealed class StubQuestionModeration : IQuestionModeration
{
    private static readonly QaControlView _live = new(
        212,
        true,
        "4f1d2c8e-9a7b-4e3f-8c21-6b5a0d9e7f12",
        QaSettings.Default with { Review = true },
        new QaSlideView(5, true, true),
        [new QaSlideView(3, false, true)],
        new QaCounts(3, 1, 1, 1),
        2,
        [
            new QaQuestion(1, "How do you split a big change?", "Priya", "12 min", QuestionState.Open, false, false, 14),
            new QaQuestion(2, "What is the smallest slice you have shipped?", null, "9 min", QuestionState.Open, false, true, 9),
            new QaQuestion(3, "Do feature flags count as shipping?", "Sam K", "8 min", QuestionState.Answered, false, false, 6),
            new QaQuestion(4, "Who reviews the slices?", null, "2 min", QuestionState.Waiting, false, false, 1),
            new QaQuestion(5, "Why is this talk so long?", null, "1 min", QuestionState.Open, true, false, 2),
            new QaQuestion(6, "How do you handle database migrations?", null, "now", QuestionState.Open, false, false, 3),
        ],
        new ReactionCounts(41, 12, 7, 3, 28));

    public Task<QaControlView> ApproveAsync(string deckId, int number) => Task.FromResult(_live);

    public Task<QaControlView> HideAsync(string deckId, int number) => Task.FromResult(_live);

    public Task<QaControlView> RestoreAsync(string deckId, int number) => Task.FromResult(_live);

    public Task<QaControlView> AnswerAsync(string deckId, int number) => Task.FromResult(_live);

    public Task<QaControlView> ReopenAsync(string deckId, int number) => Task.FromResult(_live);

    public Task<QaControlView> ShowAsync(string deckId, int number) => Task.FromResult(_live);

    public Task<QaControlView> UnshowAsync(string deckId, int number) => Task.FromResult(_live);

    public Task<QaControlView> ControlAsync(string deckId) => Task.FromResult(_live);
}
