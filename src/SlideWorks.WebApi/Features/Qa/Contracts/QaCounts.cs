namespace SlideWorks.WebApi.Features.Qa.Contracts;

// How many of the round's questions are open, waiting, answered and hidden (qa-control.json "counts", Q6), integers. A hidden
// question counts only as hidden. All zero when not live.
public sealed record QaCounts(int Open, int Waiting, int Answered, int Hidden);
