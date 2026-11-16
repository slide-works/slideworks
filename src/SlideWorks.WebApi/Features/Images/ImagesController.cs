using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Images;

// An image on a slide (K11). Open to anonymous callers (K1); IDeckAccess decides, and an image the caller may not see is
// 404, the same as an unknown one.
[Route("api/images")]
public class ImagesController(ImageUploads images) : ApiControllerBase
{
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, ImageTypes.PNG, ImageTypes.JPEG, ImageTypes.WEBP, ImageTypes.GIF)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ImageFileResult> Get(string id) => new(await images.OpenAsync(id));
}
