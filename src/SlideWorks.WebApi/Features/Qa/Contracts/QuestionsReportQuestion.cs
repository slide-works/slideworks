using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// One question in QuestionsReportSession (Q10): its text, the name shown with it (null for anonymous), its state (hidden
// wins over the state under it), its votes (an integer) and when it was asked.
public sealed record QuestionsReportQuestion(string Text, string? Name, QuestionStatus State, int Votes, DateTimeOffset AskedAt);
