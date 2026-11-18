using System.Text.Json;

namespace SlideWorks.Core.Slides;

// The built-in icon set's names (K11), read from the icons.json that SlideWorks.Core.csproj embeds from
// src/contracts. The client draws them from the same file.
public static class IconCatalogue
{
    private const string RESOURCE = "SlideWorks.Core.Slides.icons.json";

    public static IReadOnlyList<string> Names { get; } = Read();

    public static bool Has(string name) => Names.Contains(name, StringComparer.Ordinal);

    private static string[] Read()
    {
        using var stream = typeof(IconCatalogue).Assembly.GetManifestResourceStream(RESOURCE)
            ?? throw new InvalidOperationException("Missing embedded resource " + RESOURCE);
        using var document = JsonDocument.Parse(stream);
        return [.. document.RootElement.GetProperty("icons").EnumerateArray().Select(icon => icon.GetProperty("name").GetString()!)];
    }
}
