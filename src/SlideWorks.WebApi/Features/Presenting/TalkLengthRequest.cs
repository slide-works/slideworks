using System.Text.Json;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Presenting;

// The body of PUT /api/decks/{id}/talk-length (P7): { "minutes": 30 } or { "minutes": null } to clear it. Read from raw
// JSON so that a missing key, an unknown key and a string each get a message instead of quietly becoming null. Read checks
// only the body's shape (an object whose one key, minutes, is a whole number or null, the type set_talk_length's schema
// declares), before the owner check. The 1 to 600 range is Check, which TalkLengthCommands runs after the owner check, so a
// non-owner gets 404 whatever the value (D16).
public static class TalkLengthRequest
{
    public const int MIN = 1;
    public const int MAX = 600;
    public const string MINUTES = "minutes";
    public const string RANGE = "minutes: must be a whole number from 1 to 600, or null to clear the talk length";

    public static int? Read(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestException("body: must be an object such as { \"minutes\": 30 }");
        }

        JsonElement? minutes = null;
        foreach (var key in body.EnumerateObject())
        {
            if (key.Name != MINUTES)
            {
                throw new BadRequestException($"unknown key '{key.Name}'; expected minutes");
            }
            minutes = key.Value;
        }
        return minutes is null
            ? throw new BadRequestException("minutes: required; a number of minutes, or null to clear the talk length")
            : minutes.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Number when minutes.Value.TryGetInt32(out var whole) => whole,
                _ => throw new BadRequestException(RANGE),
            };
    }

    // The range rule, run by TalkLengthCommands after the owner check for both the web and set_talk_length, whose schema has
    // already made sure minutes is a whole number or null.
    public static int? Check(int? minutes)
        => minutes is null or >= MIN and <= MAX ? minutes : throw new BadRequestException(RANGE);
}
