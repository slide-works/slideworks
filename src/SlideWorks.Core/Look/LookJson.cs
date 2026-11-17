using System.Text.Json;

namespace SlideWorks.Core.Look;

// Reads the catalogue JSON that SlideWorks.Core.csproj embeds from src/contracts.
internal static class LookJson
{
    public static T Read<T>(string fileName)
    {
        var assembly = typeof(LookJson).Assembly;
        var resource = "SlideWorks.Core.Look." + fileName;
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Missing embedded resource " + resource);
        return JsonSerializer.Deserialize<T>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException(resource + " is empty");
    }
}
