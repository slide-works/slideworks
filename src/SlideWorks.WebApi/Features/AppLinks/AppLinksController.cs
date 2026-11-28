using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.AppLinks;

// The two files that let the phone app open join links (K8): iOS reads apple-app-site-association, Android reads
// assetlinks.json, and both want a plain 200 with no redirect, on the public host and the join host. A file is served only when
// its setting is set (the Apple file claims bare codes on the join host only); otherwise 404, and the links open the browser.
[AllowAnonymous]
[Route(".well-known")]
public class AppLinksController(AppLinkFiles files) : ApiControllerBase
{
    [HttpGet("apple-app-site-association")]
    [ProducesResponseType<AppleAppSiteAssociationView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<AppleAppSiteAssociationView> AppleAppSiteAssociation()
        => files.AppleAppSiteAssociation(HttpContext.Items.ContainsKey(JoinHostRouting.ON_JOIN_HOST)) is { } view ? view : NotFound();

    [HttpGet("assetlinks.json")]
    [ProducesResponseType<IReadOnlyList<AssetLinkView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<AssetLinkView>> AssetLinks() => files.AssetLinks() is { Count: > 0 } view ? Ok(view) : NotFound();
}
