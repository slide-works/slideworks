using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Look.Views;

// One font file a template brings, in a LookView (T7). Role is "head", "body" or "mono"; Style is "normal" or "italic".
// Url is root-relative, like the logo's.
public sealed record LookFontView(string Family, string Role, string Url, int Weight, string Style)
{
    public static LookFontView From(string templateRef, TemplateFont font)
        => new(font.Family, font.Role, LookAssetUrls.Of(templateRef, font.Asset), font.Weight, font.Style);
}
