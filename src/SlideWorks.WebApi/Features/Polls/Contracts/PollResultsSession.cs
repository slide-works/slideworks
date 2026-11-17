namespace SlideWorks.WebApi.Features.Polls.Contracts;

// One audience session in PollResultsView (V9): when it started and its closed rounds, oldest first.
public sealed record PollResultsSession(DateTimeOffset StartedAt, IReadOnlyList<PollResultsRound> Rounds);
