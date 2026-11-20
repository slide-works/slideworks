using System.Text.RegularExpressions;

namespace SlideWorks.Core.Look.Manifest;

// The frozen error texts of custom templates (T3), read from src/contracts/template-errors.json, which
// SlideWorks.Core.csproj embeds. A text may hold named placeholders such as {limit}.
public static partial class TemplateErrorTexts
{
    private static readonly IReadOnlyDictionary<string, string> Texts = LookJson.Read<Dictionary<string, string>>("template-errors.json");

    public static IReadOnlyCollection<string> Codes { get; } = Texts.Keys.ToList();

    // The placeholder names a code's text holds, in order.
    public static IReadOnlyList<string> Placeholders(string code) =>
        Placeholder().Matches(TextOf(code)).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();

    // The code's text with every placeholder filled. An unknown code or a placeholder without a value throws.
    public static string Format(string code, params (string Name, string Value)[] values)
    {
        var given = values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
        var text = TextOf(code);
        var missing = Placeholders(code).FirstOrDefault(name => !given.ContainsKey(name));
        if (missing is not null)
        {
            throw new ArgumentException($"The text of '{code}' needs a value for {{{missing}}}.", nameof(values));
        }

        return Placeholder().Replace(text, match => given[match.Groups[1].Value]);
    }

    private static string TextOf(string code) =>
        Texts.TryGetValue(code, out var text) ? text : throw new ArgumentException($"'{code}' is not a template error code.", nameof(code));

    [GeneratedRegex(@"\{([a-z][a-zA-Z0-9]*)\}")]
    private static partial Regex Placeholder();
}
