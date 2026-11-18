using System.Text.Json;
using System.Text.Json.Nodes;

namespace SlideWorks.Core.Slides;

// The name of one icon of the built-in set (K11). An unknown name is refused with the names nearest to it.
public sealed class IconSlot : ISlotType
{
    // What a row with an icon adds to a block's height, in whole pixels.
    public const int BLOCK_HEIGHT = 78;

    private const int MAX_NEAR_NAMES = 5;
    private const int NEAR_DISTANCE = 2;

    // An unknown name longer than this is not compared with the catalogue: no icon's name is near it.
    private const int MAX_COMPARED_LENGTH = 32;

    public string Expected => "the name of an icon";

    public string Kind => "icon";

    public JsonObject ToJsonSchema() => new()
    {
        ["type"] = "string",
        ["enum"] = new JsonArray(IconCatalogue.Names.Select(name => (JsonNode?)JsonValue.Create(name)).ToArray()),
    };

    public void Validate(JsonElement value, string path, SlotValidation validation)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            validation.Fail(path, $"expected {Expected}");
            return;
        }

        var name = value.GetString() ?? "";
        if (IconCatalogue.Has(name))
        {
            return;
        }

        var near = NearNames(name);
        var hint = near.Count == 0 ? "" : "; did you mean " + string.Join(", ", near.Select(n => $"'{n}'")) + "?";
        validation.Fail(path, $"unknown icon '{EchoText.Of(name)}'{hint}");
    }

    // Up to five names of the set near the given one: within two edits of it, or sharing its start. The
    // nearest come first.
    public static IReadOnlyList<string> NearNames(string name)
    {
        if (name.Length == 0 || name.Length > MAX_COMPARED_LENGTH)
        {
            return [];
        }

        var lowered = name.ToLowerInvariant();
        return [.. IconCatalogue.Names
            .Select(candidate => (Name: candidate, Distance: Distance(lowered, candidate)))
            .Where(c => c.Distance <= NEAR_DISTANCE)
            .OrderBy(c => c.Distance)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .Take(MAX_NEAR_NAMES)
            .Select(c => c.Name)];
    }

    // The edit distance, or 1 when one name starts with the other ('check' and 'check-circle').
    private static int Distance(string name, string candidate)
    {
        var edits = Edits(name, candidate);
        var sharesStart = name.Length >= 3
            && (candidate.StartsWith(name, StringComparison.Ordinal) || name.StartsWith(candidate, StringComparison.Ordinal));
        return sharesStart ? Math.Min(edits, 1) : edits;
    }

    private static int Edits(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var change = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(change, Math.Min(previous[j], current[j - 1]) + 1);
            }

            previous = current;
        }

        return previous[b.Length];
    }
}
