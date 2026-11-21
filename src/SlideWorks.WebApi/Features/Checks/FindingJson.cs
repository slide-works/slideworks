using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.Checks;

// The one way findings go on the wire and come back from a page: camelCase names, camelCase enum strings, no integers.
public static class FindingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };
}
