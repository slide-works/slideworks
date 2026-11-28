using System.Text.Json;

namespace SlideWorks.Core.Translations;

// The languages a deck can be translated into (K1, D10), read from src/contracts/languages.json, which
// SlideWorks.Core.csproj embeds. Nothing else spells out the code list.
public static class LanguageCatalogue
{
    private const string RESOURCE = "SlideWorks.Core.Translations.languages.json";

    private static readonly Lazy<LanguageFile> _file = new(Read);

    private static readonly Lazy<IReadOnlyList<Language>> _all = new(() => Array.AsReadOnly(_file.Value.Languages.ToArray()));

    // A read-only copy, so a caller cannot cast the list back and change the catalogue.
    public static IReadOnlyList<Language> All => _all.Value;

    // The language with this code, compared case-sensitively, or null when it is not supported.
    public static Language? Find(string code) =>
        All.FirstOrDefault(language => string.Equals(language.Code, code, StringComparison.Ordinal));

    // True for a right-to-left language (ar, he, fa, ur), which is refused with its own message.
    public static bool IsRightToLeft(string code) => _file.Value.RightToLeft.Contains(code, StringComparer.Ordinal);

    // The script of a supported language, or null when the code is not supported.
    public static LanguageScript? ScriptOf(string code) =>
        Find(code) is { } language ? _file.Value.Scripts[language.Script] : null;

    private static LanguageFile Read()
    {
        using var stream = typeof(LanguageCatalogue).Assembly.GetManifestResourceStream(RESOURCE)
            ?? throw new InvalidOperationException("Missing embedded resource " + RESOURCE);
        return JsonSerializer.Deserialize<LanguageFile>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException(RESOURCE + " is empty");
    }
}
