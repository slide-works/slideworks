namespace SlideWorks.WebApi.Features.Look.Views;

// Where a template's file is read (T6): root-relative, never built with IPublicUrls, so a headless browser loads it from
// the host it renders on.
public static class LookAssetUrls
{
    public static string Of(string templateRef, string assetId) => $"/api/templates/{templateRef}/assets/{assetId}";
}
