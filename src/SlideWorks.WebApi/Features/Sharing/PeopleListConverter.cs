using System.Text.Json;
using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Sharing.Views;

namespace SlideWorks.WebApi.Features.Sharing;

// The PUT body's people, read like ViewerListConverter reads viewers: it stops at the first entry past
// ViewerEmails.MAX_ENTRIES, so an oversized list is never fully deserialised. Each entry is read by the default reader.
// SharingBodyErrorsAttribute turns the refusal into 400 { "error": "people: at most 50 emails" }.
public sealed class PeopleListConverter : JsonConverter<IReadOnlyList<SharingPersonUpdate>>
{
    public override IReadOnlyList<SharingPersonUpdate> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException();
        }
        var people = new List<SharingPersonUpdate>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (people.Count == ViewerEmails.MAX_ENTRIES)
            {
                throw new JsonException(ViewerEmails.PEOPLE_TOO_MANY);
            }
            people.Add(ReadPerson(ref reader, options));
        }
        return people;
    }

    // The default reader's own errors name no field once they leave this converter, so they are renamed here and
    // SharingBodyErrorsAttribute can tell the body's people are what failed.
    private static SharingPersonUpdate ReadPerson(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<SharingPersonUpdate>(ref reader, options) ?? throw new JsonException("people: entry is null");
        }
        catch (JsonException e) when (e.Message != ViewerEmails.PEOPLE_TOO_MANY && !e.Message.Contains("people", StringComparison.Ordinal))
        {
            throw new JsonException("people: entry is invalid", e);
        }
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<SharingPersonUpdate> value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value.ToList(), options);
}
