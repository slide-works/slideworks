namespace SlideWorks.WebApi.Features.Polls.Contracts;

// A round's tally as IPollTallies.Read returns it (V5): how many guests have answered and the count for each option, by
// position. With pick several, the counts can add up to more than Answered.
public sealed record PollCounts(int Answered, IReadOnlyList<int> Counts);
