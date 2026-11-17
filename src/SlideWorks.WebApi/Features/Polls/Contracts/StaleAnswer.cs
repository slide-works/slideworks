using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Polls.Contracts;

// The body of a 409 to PUT /api/join/{code}/poll/answer (poll-answer.json "stale", V5): the round isn't the open one, the
// revision doesn't match or the poll isn't open, nothing was written, and Current is what the phone should show now.
public sealed record StaleAnswer(GuestPollView Current)
{
    public const string ERROR = "stale";

    [JsonPropertyOrder(-1)]
    public string Error => ERROR;
}
