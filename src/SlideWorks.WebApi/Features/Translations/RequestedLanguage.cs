using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Translations;

// A language an owner's tool asked for by name (get_deck, the check tools): null for no language or the primary, else one of
// the deck's languages while translations are on. Anything else is refused, naming the languages the deck has. The viewer
// routes never call this: they read an unknown language as the primary (TranslationMerge.For).
public static class RequestedLanguage
{
    public static string? For(TranslationSettings settings, string? language)
    {
        if (language is null || string.Equals(language, settings.Primary, StringComparison.Ordinal))
        {
            return null;
        }

        var offered = settings.Enabled ? settings.Languages : [];
        if (!offered.Contains(language, StringComparer.Ordinal))
        {
            throw new BadRequestException($"{language} is not one of this deck's languages ({string.Join(", ", offered.Prepend(settings.Primary))})");
        }

        return language;
    }
}
