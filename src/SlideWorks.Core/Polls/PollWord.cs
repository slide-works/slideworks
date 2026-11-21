namespace SlideWorks.Core.Polls;

// One word in an open question's cloud, and how many answers used it. The text is the word as counted, lower case.
public sealed record PollWord(string Text, int Count);
