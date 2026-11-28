using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Features.AppLinks;

// One Digital Asset Links statement: the Android app with this package and signing certificate may handle this site's links.
public sealed record AssetLinkView(
    [property: JsonPropertyName("relation")] IReadOnlyList<string> Relation,
    [property: JsonPropertyName("target")] AssetLinkTargetView Target)
{
    public static AssetLinkView From(string package, IReadOnlyList<string> fingerprints)
        => new(["delegate_permission/common.handle_all_urls"], new AssetLinkTargetView("android_app", package, fingerprints));
}
