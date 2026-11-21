using System.Text.RegularExpressions;
using System.Text.Json;

namespace SlideWorks.WebApi.Features.Checks;

// Alt text that names a kind of file or picture instead of saying what it shows.
public static partial class GenericAlt
{
    private static readonly HashSet<string> WORDS = new(
        ["image", "picture", "photo", "photograph", "img", "screenshot", "graphic", "icon", "logo", "alt", "untitled"],
        StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[\w\-. ]+\.(png|jpe?g|gif|webp|svg)$", RegexOptions.IgnoreCase)]
    private static partial Regex FileName();

    public static bool IsGeneric(string alt, string imageId)
    {
        var trimmed = alt.Trim();
        return WORDS.Contains(trimmed) || FileName().IsMatch(trimmed) || string.Equals(trimmed, imageId, StringComparison.OrdinalIgnoreCase);
    }

    // The path of every {image, alt} object in the slots, at any depth, whose alt is generic: "image.image", "two-column.left.image".
    public static IReadOnlyList<string> Paths(JsonElement slots, string layout)
    {
        var found = new List<string>();
        Walk(slots, layout, found);
        return found;
    }

    private static void Walk(JsonElement element, string path, List<string> found)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, $"{path}[{index++}]", found);
            }
            return;
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        if (element.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.String
            && element.TryGetProperty("alt", out var alt) && alt.ValueKind == JsonValueKind.String
            && IsGeneric(alt.GetString()!, image.GetString()!))
        {
            found.Add(path);
        }
        foreach (var property in element.EnumerateObject())
        {
            Walk(property.Value, $"{path}.{property.Name}", found);
        }
    }
}
