namespace SlideWorks.WebApi.Features.Qa.Contracts;

// What get_questions returns (questions-report.json, Q10): each audience session's questions, oldest session first. Never
// which guest asked or upvoted.
public sealed record QuestionsReport(IReadOnlyList<QuestionsReportSession> Sessions);
