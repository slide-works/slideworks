using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Features.Decks;

namespace SlideWorks.WebApi.Features.Templates.Drafts;

// The short name (T3) a new template gets from its label: lower-case letters, digits and hyphens, at most
// ManifestJson.MAX_NAME characters.
public static class ShortNames
{
    public static string Of(string label)
    {
        var slug = DeckIds.Slugify(label);
        if (slug.Length > ManifestJson.MAX_NAME)
        {
            var cut = slug.LastIndexOf('-', ManifestJson.MAX_NAME);
            slug = cut > 0 ? slug[..cut] : slug[..ManifestJson.MAX_NAME];
        }
        return slug.TrimEnd('-');
    }
}
