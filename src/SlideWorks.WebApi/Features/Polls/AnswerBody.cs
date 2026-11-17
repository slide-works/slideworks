using System.Text.Json;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// The body of PUT /api/join/{code}/poll/answer (poll-answer.json "request", V5), read from raw JSON so that a missing member
// gets V5's { "error": "…" } instead of a ProblemDetails or a quiet default: a missing revision must not become 0, a first
// answer. Only the body's shape is checked here; the picks' rules (pick one, at most, unknown option, duplicate, none) are
// IPollAnswers', which knows the poll.
public static class AnswerBody
{
    public const string ROUND_REQUIRED = "round: required";
    public const string PICKS_REQUIRED = "picks: required";
    public const string REVISION_REQUIRED = "revision: required";
    public const string BODY_INVALID = "body: expected JSON { round, picks, revision }";

    public static AnswerRequest Read(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestException(BODY_INVALID);
        }
        var round = Member(body, "round", ROUND_REQUIRED);
        var picks = Member(body, "picks", PICKS_REQUIRED);
        var revision = Member(body, "revision", REVISION_REQUIRED);
        if (round.ValueKind != JsonValueKind.String
            || picks.ValueKind != JsonValueKind.Array
            || picks.EnumerateArray().Any(pick => !IsWhole(pick))
            || !IsWhole(revision))
        {
            throw new BadRequestException(BODY_INVALID);
        }
        return new AnswerRequest(round.GetString()!, [.. picks.EnumerateArray().Select(pick => pick.GetInt32())], revision.GetInt32());
    }

    // The member, or the refusal when it is missing or null.
    private static JsonElement Member(JsonElement body, string name, string required)
        => body.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : throw new BadRequestException(required);

    private static bool IsWhole(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _);
}
