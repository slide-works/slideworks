using System.Text.Json;
using SlideWorks.Core.Slides;
using SlideWorks.Core.Translations;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Translations;

// Re-keys a slide's stored lines after update_slide replaced its slots (SW-883 K4, D13), and words the warnings about lines that
// are now out of date or were removed. While translations are off nothing changes and nothing is said. Every stored language
// is re-keyed, so a language taken off the list (D14) still fits the slide when it comes back; warnings name only the
// languages on the list, in its order.
public static class StaleWarnings
{
    public static IReadOnlyList<string> Rekey(Slide slide, LayoutDefinition layout, JsonElement slots, TranslationSettings settings, int number)
    {
        if (!settings.Enabled || slide.TranslationsJson is null)
        {
            return [];
        }

        var stored = SlideTranslationsJson.Read(slide.TranslationsJson);
        var rekeyed = stored.ToDictionary(language => language.Key, language => SlideTranslation.Rekey(layout, slots, language.Value), StringComparer.Ordinal);
        slide.TranslationsJson = SlideTranslationsJson.Write(rekeyed.ToDictionary(language => language.Key, language => language.Value.Entries, StringComparer.Ordinal));

        var listed = settings.Languages.Where(rekeyed.ContainsKey).Select(code => (Code: code, Result: rekeyed[code])).ToList();
        var warnings = new List<string>(2);
        var stale = listed.Where(language => language.Result.Stale.Count > 0).ToList();
        if (stale.Count > 0)
        {
            var lines = TranslatableLines.Of(layout, slots).Select(line => line.Path);
            var paths = lines.Where(path => stale.Any(language => language.Result.Stale.Contains(path, StringComparer.Ordinal)));
            var one = stale.Sum(language => language.Result.Stale.Count) == 1;
            warnings.Add($"slide {number}: {Counts(stale.Select(language => (language.Code, language.Result.Stale.Count)))} {(one ? "is" : "are")} out of date ({string.Join(", ", paths)}); translate {(one ? "it" : "them")} again with translate_slides");
        }

        var removed = listed.Where(language => language.Result.Removed.Count > 0).ToList();
        if (removed.Count > 0)
        {
            var one = removed.Sum(language => language.Result.Removed.Count) == 1;
            warnings.Add($"slide {number}: {Counts(removed.Select(language => (language.Code, language.Result.Removed.Count)))} {(one ? "no longer exists and was" : "no longer exist and were")} removed");
        }

        return warnings;
    }

    // "2 Thai lines and 1 Japanese line", or "1 Thai line, 2 Korean lines and 1 Japanese line".
    private static string Counts(IEnumerable<(string Code, int Count)> languages)
    {
        var parts = languages.Select(language => $"{language.Count} {EnglishName(language.Code)} {(language.Count == 1 ? "line" : "lines")}").ToList();
        return parts.Count == 1 ? parts[0] : $"{string.Join(", ", parts[..^1])} and {parts[^1]}";
    }

    private static string EnglishName(string code) =>
        (LanguageCatalogue.Find(code) ?? throw new InvalidOperationException($"A deck offers {code}, which is not in the language catalogue.")).EnglishName;
}
