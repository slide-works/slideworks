using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Accounts.Profile;
using SlideWorks.WebApi.Features.Accounts.Profile.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Accounts;

// The profile page's wire (C3), approved people only: a pending person gets 403 pending_approval from the default policy.
[Route("api/me")]
public class ProfileController(ProfileViews views, ProfileActions actions) : ApiControllerBase
{
    [HttpGet("profile")]
    [ProducesResponseType<ProfileView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<ProfileView> Get() => views.GetAsync();

    [HttpPut("profile")]
    [ProducesResponseType<ProfileView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<ProfileView> Put(ProfileUpdate update) => actions.UpdateAsync(update);

    [HttpPut("time-zone")]
    [ProducesResponseType<ProfileView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<ProfileView> PutTimeZone(TimeZoneUpdate update) => actions.SetTimeZoneAsync(update);

    // The raw image bytes, as upload_image's URL takes them: no multipart.
    [HttpPost("photo")]
    [ProducesResponseType<ProfileView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    public Task<ProfileView> PostPhoto() => actions.UploadPhotoAsync(Request.Body, Request.ContentType, HttpContext.RequestAborted);

    [HttpDelete("photo")]
    [ProducesResponseType<ProfileView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    public Task<ProfileView> DeletePhoto() => actions.RemovePhotoAsync();
}
