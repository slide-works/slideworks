using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.RequestLimits;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// The upload URL a started upload hands out (T6). Anonymous, because the token in the path is the credential: the raw
// bytes are PUT with no cookie and no Origin, which CrossSiteWriteGuard lets through as it does any request that isn't
// signed in. The body's Content-Type is not read: what the file is comes from its check.
[Route(TemplateAssets.UPLOAD_ROUTE)]
[TemplateErrors]
public class TemplateUploadsController(ITemplateAssets assets) : ApiControllerBase
{
    [HttpPut("{token}")]
    [AllowAnonymous]
    [OwnsBodyLimit]
    [ProducesResponseType<StoredAsset>(StatusCodes.Status200OK)]
    [ProducesResponseType<TemplateErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<StoredAsset> Put(string token) => assets.CompleteAsync(token, Request.Body, HttpContext.RequestAborted);
}
