using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Presenting.Mcp;

// set_talk_length's result (P7): the deck's talk length after the change, in minutes, or null when it is cleared.
public sealed record SetTalkLengthResult([property: JsonPropertyName("talk_minutes")] int? TalkMinutes);
