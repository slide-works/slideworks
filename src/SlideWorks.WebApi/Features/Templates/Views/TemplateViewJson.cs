using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Templates.Views;

// Reads a template view from JSON in the wire format (camelCase names and enum values).
public static class TemplateViewJson
{
    private static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static T Read<T>(string json)
        => JsonSerializer.Deserialize<T>(json, JSON) ?? throw new InvalidOperationException($"The JSON for {typeof(T).Name} is null.");
}
