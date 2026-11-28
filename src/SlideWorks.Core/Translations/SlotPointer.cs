using System.Globalization;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Translations;

// RFC 6901 pointers over a slide's stored slots: "/items/0/text".
internal static class SlotPointer
{
    public static string Append(string pointer, string name) =>
        pointer + "/" + name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    public static string Append(string pointer, int index) => pointer + "/" + index.ToString(CultureInfo.InvariantCulture);

    // Sets the string leaf the pointer names. The pointer comes from TranslatableLines over the same slots,
    // so every step exists.
    public static void Replace(JsonNode root, string pointer, string text)
    {
        var tokens = pointer.Split('/').Skip(1).Select(Unescape).ToArray();
        var parent = root;
        foreach (var token in tokens[..^1])
        {
            parent = Step(parent, token);
        }

        var last = tokens[^1];
        if (parent is JsonArray array)
        {
            array[int.Parse(last, CultureInfo.InvariantCulture)] = text;
        }
        else
        {
            parent[last] = text;
        }
    }

    private static JsonNode Step(JsonNode node, string token) => node is JsonArray array
        ? array[int.Parse(token, CultureInfo.InvariantCulture)]!
        : node[token]!;

    private static string Unescape(string token) =>
        token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
}
