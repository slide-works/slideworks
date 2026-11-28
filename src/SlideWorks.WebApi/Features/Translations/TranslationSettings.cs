namespace SlideWorks.WebApi.Features.Translations;

// A deck's translation settings (SW-883 K2). Languages never contains Primary.
public record TranslationSettings(bool Enabled, string Primary, IReadOnlyList<string> Languages)
{
    // A deck with no settings row: off, English, no languages.
    public static TranslationSettings Default { get; } = new(false, "en", []);
}
