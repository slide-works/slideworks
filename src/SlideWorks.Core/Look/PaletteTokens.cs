using System.Globalization;
using System.Text.Json;

namespace SlideWorks.Core.Look;

// Custom colours on a deck: a few tokens of a named palette, replaced, without a template of its own.
public static class PaletteTokens
{
    public static Palette Apply(Palette palette, string? json)
    {
        var overrides = Read(json);
        if (overrides.Count == 0)
        {
            return palette;
        }

        var tokens = new Dictionary<string, string>(palette.Tokens, StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
        {
            tokens[key] = value;
        }

        return palette with { Tokens = tokens };
    }

    // Null when the overrides may be stored. The first problem otherwise, naming the token.
    public static string? Check(Palette palette, IReadOnlyDictionary<string, string> colors)
    {
        foreach (var (key, value) in colors)
        {
            if (!palette.Tokens.ContainsKey(key))
            {
                return $"colors.{key}: unknown colour";
            }

            if (value is null || value.Length != 7 || !Contrast.IsColour(value))
            {
                return $"colors.{key}: a colour is # and six hex digits";
            }
        }

        var merged = Apply(palette, Write(colors));
        var failed = PaletteContrast.Check(merged).FirstOrDefault(check => !check.Pass);
        return failed is null
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"colors: {failed.Pair} is {failed.Ratio:0.0}:1, under the {failed.Bar:0.0}:1 it needs");
    }

    public static string? Write(IReadOnlyDictionary<string, string>? colors) =>
        colors is { Count: > 0 } ? JsonSerializer.Serialize(colors) : null;

    public static IReadOnlyDictionary<string, string> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }
}
