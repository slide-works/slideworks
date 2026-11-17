namespace SlideWorks.WebApi.Features.Qa.Contracts;

// One audience session in QuestionsReport (Q10): when it started, how many questions were asked, answered and hidden
// (integers), and its questions in the order they were asked.
public sealed record QuestionsReportSession(DateTimeOffset StartedAt, int Asked, int Answered, int Hidden, IReadOnlyList<QuestionsReportQuestion> Questions);
