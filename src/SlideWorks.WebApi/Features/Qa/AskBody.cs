namespace SlideWorks.WebApi.Features.Qa;

// The body of POST /api/join/{code}/questions (question-ask.json "request", Q4), bound by the framework as an AskRequest once
// BodyLimitAttribute has read at most MAX_BYTES of it (else 413). A missing text or name binds as null: the text and name
// rules (required, at most 200, at most 30, not allowed) are IQuestionAsking's, through QuestionText.
public static class AskBody
{
    // The largest body the ask route reads. A question of 200 characters and a name of 30, each of them 4-byte characters
    // escaped as JSON, is under 2 KB with the members' names.
    public const int MAX_BYTES = 2048;
}
