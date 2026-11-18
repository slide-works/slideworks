using System.Text.Json;

namespace SlideWorks.Core.Look.CustomLayouts;

// Reading stored custom-layout JSON so that anything outside the contract is a JsonException, never
// a missing-key or wrong-kind exception. No message quotes a piece of the document.
internal static class StrictJson
{
    public static void OnlyKeys(JsonElement json, string[] keys, string what)
    {
        if (json.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"{what} is made of objects.");
        }

        var names = json.EnumerateObject().Select(property => property.Name).ToList();
        if (names.Exists(name => !keys.Contains(name, StringComparer.Ordinal)) || names.Distinct(StringComparer.Ordinal).Count() != names.Count)
        {
            throw new JsonException($"{what} has a key that is unknown or written twice.");
        }
    }

    public static JsonElement Member(JsonElement json, string key, string what) =>
        json.TryGetProperty(key, out var value) ? value : throw new JsonException($"{what} needs '{key}'.");

    public static string Text(JsonElement json, string what) =>
        json.ValueKind == JsonValueKind.String ? json.GetString()! : throw new JsonException($"{what} holds a string where the contract has one.");

    public static int Whole(JsonElement json, string what) =>
        json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out var number) ? number : throw new JsonException($"{what} holds a whole number where the contract has one.");
}
