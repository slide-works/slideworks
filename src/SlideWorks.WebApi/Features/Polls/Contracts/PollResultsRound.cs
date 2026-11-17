namespace SlideWorks.WebApi.Features.Polls.Contracts;

// One closed round in PollResultsSession (V9, V10): how many answered, how many had joined when it closed, the response
// rate (Answered ÷ Joined) and the final count for each option.
public sealed record PollResultsRound(int Number, int Answered, int Joined, double Rate, IReadOnlyList<int> Counts);
