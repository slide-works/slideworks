namespace SlideWorks.WebApi.Features.Polls.Data;

// A round's result as it stood when the round closed (V2), stored as JSON on the round: the option texts it ran with, the
// count for each by position, and how many guests answered. Closed rounds keep their own texts (D13), so a later reword or
// an added option never changes them.
public sealed record PollFinal(IReadOnlyList<string> Options, IReadOnlyList<int> Counts, int Answered);
