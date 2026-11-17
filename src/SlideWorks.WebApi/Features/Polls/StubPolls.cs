using Agoda.IoC.Core;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// TODO: replace with the real round lifecycle and the presenter's view read from the poll's rows.
// Answers every slide with the open sample of poll-control.json, and every action returns it unchanged.
[RegisterPerRequest(For = typeof(IPolls))]
public sealed class StubPolls : IPolls
{
    private static readonly IReadOnlyList<string> _options = ["Under 50 lines", "50 to 200 lines", "200 to 1,000 lines", "Over 1,000 lines"];

    private static readonly PollControlView _open = new(
        131,
        4,
        "5d2e9f81-73a4-4c1b-b6e0-2f8a9c4d1e37",
        2,
        PollPhase.Open,
        new PollSettings(PollPick.One, null, PollResults.AfterClose, null, PollOpens.OnArrival, PollPrivacy.Private),
        "How big was your last merge request?",
        _options,
        21,
        41,
        [5, 9, 4, 3],
        false,
        false,
        false,
        [new PollRoundSummary(1, 38, 41)]);

    public Task<PollControlView> OpenAsync(string deckId, int slide) => Task.FromResult(_open);

    public Task<PollControlView> CloseAsync(string deckId, int slide) => Task.FromResult(_open);

    public Task<PollControlView> RevealAsync(string deckId, int slide) => Task.FromResult(_open);

    public Task<PollControlView> ShowAnswerAsync(string deckId, int slide) => Task.FromResult(_open);

    public Task<PollControlView> NewRoundAsync(string deckId, int slide) => Task.FromResult(_open);

    public Task<PollControlView?> ControlAsync(string deckId, int slide) => Task.FromResult<PollControlView?>(_open);
}
