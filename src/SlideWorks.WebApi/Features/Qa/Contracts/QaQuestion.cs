using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// One question in the presenter's QaControlView (qa-control.json, Q6): every question of the round, in number order (the
// panel sorts). Number is its 1-based position in the round; State is the stored state, with Hidden beside it so Restore
// returns it; Name is null for an anonymous question; Age is the server's label; Votes is an integer. Never which guest
// asked or upvoted.
public sealed record QaQuestion(int Number, string Text, string? Name, string Age, QuestionState State, bool Hidden, bool OnScreen, int Votes);
