using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The body of a 429 to POST /api/join/{code}/questions (question-ask.json "tooManyQuestions", Q4): RetryAfter is whole
// seconds until the guest can ask again, an integer.
public sealed record TooManyQuestions(int RetryAfter)
{
    [JsonPropertyOrder(-1)]
    public string Error => GuestQaErrors.TOO_MANY_QUESTIONS;
}
