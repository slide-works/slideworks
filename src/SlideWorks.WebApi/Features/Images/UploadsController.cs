using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Images;

// The upload URL upload_image hands out (K1, K11). Anonymous, because the token in the path is the credential: an agent
// PUTs the raw image bytes with no cookie and no Origin, which CrossSiteWriteGuard lets through as it does any request
// that isn't signed in.
[Route("api/uploads")]
public class UploadsController(ImageUploads images) : ApiControllerBase
{
    [HttpPut("{token}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    public Task<NoContentResult> Put(string token) => NoContentAfter(images.CompleteAsync(token, Request.Body, Request.ContentLength, HttpContext.RequestAborted));
}
