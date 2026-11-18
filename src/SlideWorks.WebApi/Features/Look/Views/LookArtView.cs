using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Look.Views;

// The title art in a LookView (T7). Kind is "none", "generator" (Generator names it) or "image" (Url is the file).
public sealed record LookArtView(string Kind, string? Generator, string? Url)
{
    public static LookArtView From(string templateRef, TemplateLook look)
        => new(look.ArtKind, look.ArtGenerator, look.ArtAsset is null ? null : LookAssetUrls.Of(templateRef, look.ArtAsset));
}
