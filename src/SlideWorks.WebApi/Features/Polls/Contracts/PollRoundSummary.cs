namespace SlideWorks.WebApi.Features.Polls.Contracts;

// One closed round in PollControlView.Rounds (V4): its number, how many answered and how many had joined when it closed.
public sealed record PollRoundSummary(int Number, int Answered, int Joined);
