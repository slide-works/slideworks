using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Polls.Contracts;

// The body of PUT /api/join/{code}/poll/answer (poll-answer.json, V5): the round the guest answers, the 0-based option
// positions it picked, and the revision of the answer it holds (0 for a first answer). Text is the free answer on a
// text or word-cloud question, and is left off a choice poll.
public sealed record AnswerRequest(
    string Round,
    IReadOnlyList<int> Picks,
    int Revision,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null);
