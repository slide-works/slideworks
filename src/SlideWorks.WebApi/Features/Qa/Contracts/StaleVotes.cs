using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The body of a 409 "stale" to PUT /api/join/{code}/questions/votes (question-votes.json "stale", Q4): nothing was written,
// and Current is the guest's view now.
public sealed record StaleVotes(GuestQuestionsView Current)
{
    [JsonPropertyOrder(-1)]
    public string Error => GuestQaErrors.STALE;
}
