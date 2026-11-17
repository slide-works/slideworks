namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The body of POST /api/join/{code}/questions (question-ask.json "request", Q4): the question and the name to show with it,
// null for anonymous. QuestionText cleans and checks both.
public sealed record AskRequest(string? Text, string? Name);
