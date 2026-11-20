using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.OpenResponses.Contracts;

// The body of PUT /api/join/{code}/slides/{slide}/words (word-cloud.json "request"): the round and revision the phone saw,
// and up to 3 words as typed.
public record WordsRequest(string Round, [property: JsonRequired] int Revision, IReadOnlyList<string> Words);
