using System.Text.Json;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// The body of PUT /api/join/{code}/poll/answer (poll-answer.json "request", V5), read from the raw request body so that every
// failure gets V5's { "error": "…" } instead of a ProblemDetails or a quiet default: a missing revision must not become 0, a
// first answer. At most MAX_BYTES are read, as ImageStore counts an upload: a longer body is 413 and the rest is never read. Only the body's shape is checked here; the picks' rules (pick one, at most, unknown option, duplicate, none) are
// IPollAnswers', which knows the poll. Unknown members are ignored. More picks than a poll can have options is refused
// before any is read, so a long list is never copied.
public static class AnswerBody
{
    public const string ROUND_REQUIRED = "round: required";
    public const string PICKS_REQUIRED = "picks: required";
    public const string REVISION_REQUIRED = "revision: required";
    public const string BODY_INVALID = "body: expected JSON { round, picks, revision }";
    public const string TOO_MANY_PICKS = "picks: at most 8";
    public const string TOO_LARGE = "body: at most 1024 bytes";

    // The largest body the answer route reads (413 above it). A real answer is under 100 bytes.
    public const int MAX_BYTES = 1024;

    // The poll layout's most options (LayoutCatalogue's poll slot, 2 to 8).
    public const int MAX_PICKS = 8;

    public static async Task<AnswerRequest> ReadAsync(Stream body)
    {
        var buffer = new byte[MAX_BYTES + 1];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await body.ReadAsync(buffer.AsMemory(length))) > 0)
        {
            length += read;
        }
        if (length > MAX_BYTES)
        {
            throw new PayloadTooLargeException(TOO_LARGE);
        }
        return Check(Parse(buffer.AsMemory(0, length)));
    }

    // The JSON, or the body's refusal when it is empty or isn't JSON.
    private static JsonElement Parse(ReadOnlyMemory<byte> utf8)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new BadRequestException(BODY_INVALID);
        }
    }

    private static AnswerRequest Check(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestException(BODY_INVALID);
        }
        var round = Member(body, "round", ROUND_REQUIRED);
        var picks = Member(body, "picks", PICKS_REQUIRED);
        var revision = Member(body, "revision", REVISION_REQUIRED);
        if (picks.ValueKind == JsonValueKind.Array && picks.GetArrayLength() > MAX_PICKS)
        {
            throw new BadRequestException(TOO_MANY_PICKS);
        }
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
