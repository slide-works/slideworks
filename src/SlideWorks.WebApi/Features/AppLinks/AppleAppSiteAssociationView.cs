using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.AppLinks;

// Apple's apple-app-site-association file: the app may open /j/<code> on any host, and on the join host also the bare six-character
// code. The public host never claims a six-character path, because the SPA has root routes of that length (such as /decks).
public sealed record AppleAppSiteAssociationView([property: JsonPropertyName("applinks")] AppleAppLinksView AppLinks)
{
    public static AppleAppSiteAssociationView From(string appId, bool onJoinHost)
    {
        List<AppleAppLinkComponentView> components = [new("/j/*")];
        if (onJoinHost)
        {
            components.Add(new("/??????"));
        }
        return new(new AppleAppLinksView([], [new AppleAppLinkDetailView([appId], components)]));
    }
}

public sealed record AppleAppLinksView(
    [property: JsonPropertyName("apps")] IReadOnlyList<string> Apps,
    [property: JsonPropertyName("details")] IReadOnlyList<AppleAppLinkDetailView> Details);

public sealed record AppleAppLinkDetailView(
    [property: JsonPropertyName("appIDs")] IReadOnlyList<string> AppIds,
    [property: JsonPropertyName("components")] IReadOnlyList<AppleAppLinkComponentView> Components);

public sealed record AppleAppLinkComponentView([property: JsonPropertyName("/")] string Path);
