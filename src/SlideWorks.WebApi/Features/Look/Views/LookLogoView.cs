namespace SlideWorks.WebApi.Features.Look.Views;

// A deck's logo in a LookView (L6). Url is root-relative (/api/images/{id}), never built with IPublicUrls, so a
// headless browser loads it from the host it renders on.
public sealed record LookLogoView(string Url)
{
    public static LookLogoView? From(string? imageId) => imageId is null ? null : new LookLogoView($"/api/images/{imageId}");
}
