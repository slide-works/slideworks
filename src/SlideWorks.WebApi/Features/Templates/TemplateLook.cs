using SlideWorks.Core.Look;

namespace SlideWorks.WebApi.Features.Templates;

// What a template brings beyond Core's Template (T4): fonts, logos, title art, ornament colours, the top bar and extra
// colours. Logo, art and font values are asset ids, never urls. ArtKind is "none", "generator" or "image"; TopBar is
// "none" or "accent".
public sealed record TemplateLook(
    IReadOnlyList<TemplateFont> Fonts,
    string? LightLogo,
    string? DarkLogo,
    bool ShowLogoOnDark,
    string ArtKind,
    string? ArtGenerator,
    string? ArtAsset,
    IReadOnlyList<string> OrnamentColours,
    string TopBar,
    IReadOnlyList<ExtraColour> Extras)
{
    public const string NO_ART = "none";
    public const string GENERATOR_ART = "generator";
    public const string IMAGE_ART = "image";
    public const string NO_TOP_BAR = "none";
    public const string ACCENT_TOP_BAR = "accent";

    // A template that brings nothing of its own.
    public static TemplateLook None { get; } = new([], null, null, false, NO_ART, null, null, [], NO_TOP_BAR, []);

    // A built-in's look: nothing of its own but its title art generator, when it has one. OrnamentColours stays empty,
    // so the title slide draws the dots it draws today.
    public static TemplateLook OfBuiltIn(Template template)
        => template.TitleArt is null ? None : None with { ArtKind = GENERATOR_ART, ArtGenerator = template.TitleArt };
}
