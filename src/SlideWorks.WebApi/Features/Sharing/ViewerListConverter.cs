using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Sharing;

// Reads the PUT body's viewers and stops at the first entry past ViewerEmails.MAX_ENTRIES, so an oversized list costs
// no more than a short one: the rest of the body is never parsed into strings. SharingBodyErrorsAttribute turns the
// refusal into 400 { "error": "viewers: at most 50 emails" }. Anything but an array of strings (or nulls, which
// ViewerEmails then names as not an email) fails as a plain type error.
public sealed class ViewerListConverter : JsonConverter<IReadOnlyList<string>>
{
    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException();
        }
        var viewers = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.Null))
            {
                throw new JsonException();
            }
            if (viewers.Count == ViewerEmails.MAX_ENTRIES)
            {
                throw new JsonException(ViewerEmails.TOO_MANY);
            }
            viewers.Add(reader.GetString()!);
        }
        return viewers;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var viewer in value)
        {
            writer.WriteStringValue(viewer);
        }
        writer.WriteEndArray();
    }
}
