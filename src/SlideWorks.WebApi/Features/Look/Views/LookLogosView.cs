using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Look.Views;

// The template's own logo pair in a LookView (T7): one for light slides, one for dark, each a url or null. ShowOnDark
// is whether dark slides carry a logo at all. The deck's own logo is LookView.Logo.
public sealed record LookLogosView(string? Light, string? Dark, bool ShowOnDark)
{
    public static LookLogosView From(string templateRef, TemplateLook look)
        => new(
            look.LightLogo is null ? null : LookAssetUrls.Of(templateRef, look.LightLogo),
            look.DarkLogo is null ? null : LookAssetUrls.Of(templateRef, look.DarkLogo),
            look.ShowLogoOnDark);
}
