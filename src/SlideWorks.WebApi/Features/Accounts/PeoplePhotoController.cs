using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Accounts.Profile;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity.Gate;

namespace SlideWorks.WebApi.Features.Accounts;

// A person's uploaded photo (C3, D21): any signed-in person, pending included, so a pending person's own avatar still loads.
// Only the person's current photo id answers; an old or unknown one is 404. This is not a people lookup: both ids are needed.
[Route("api/people/{userId:guid}/photo")]
public class PeoplePhotoController(IProfilePhotos photos) : ApiControllerBase
{
    [HttpGet("{photoId}")]
    [Authorize(Policy = SignedInPolicy.NAME)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid userId, string photoId)
    {
        var photo = await photos.OpenAsync(userId, photoId) ?? throw new NotFoundException("Photo not found");
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo.Content, photo.ContentType);
    }
}
