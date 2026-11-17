namespace SlideWorks.WebApi.Features.Polls.Contracts;

// One audience session in PollResultsView (V9): when it started and its rounds, oldest first.
public sealed record PollResultsSession(DateTimeOffset StartedAt, IReadOnlyList<PollResultsRound> Rounds);
