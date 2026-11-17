namespace SlideWorks.Core.Qa;

// A count of taps for each of the five reactions (Q6, Q7): a reactions event's batch, or a session's totals. Every count is
// an integer.
public sealed record ReactionCounts(int ThumbsUp, int Heart, int Laugh, int Wow, int Clap)
{
    public static ReactionCounts Zero { get; } = new(0, 0, 0, 0, 0);
}
