using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Images;

// The browser's image upload, for the Look panel's logo (L6): the same upload path as upload_image. The deck's owner only;
// anyone else gets 404. The bytes then go to the returned uploadUrl with PUT /api/uploads/{token}.
[Route("api/decks/{id}/images")]
public class DeckImagesController(ImageUploads uploads) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<UploadImageResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ObjectResult> Create(string id, ImageUploadRequest request)
        => StatusCode(StatusCodes.Status201Created, await uploads.CreateAsync(id, request.Name, request.ContentType, tool: null));
}
