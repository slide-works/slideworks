namespace SlideWorks.WebApi.Features.Polls.Contracts;

// One round in PollResultsSession (V9, V10): the option texts its counts belong to (a closed round's from its Final, an open
// round's the slide's current ones), how many answered, how many had joined (when it closed, or now for an open round), the
// response rate (Answered ÷ Joined) and the count for each of those options. Counts always has as many entries as Options.
public sealed record PollResultsRound(int Number, IReadOnlyList<string> Options, int Answered, int Joined, double Rate, IReadOnlyList<int> Counts);
