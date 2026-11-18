using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates.Mcp;

// The address of a template's editor, absolute and without a token.
public static class TemplateUrls
{
    public static string Editor(IPublicUrls urls, string id) => $"{urls.Base}/templates/{id}/edit";
}
