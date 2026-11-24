using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Sharing.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.RequestLimits;

namespace SlideWorks.WebApi.Features.Sharing;

// The sharing dialog (K4 sharing.json): the owner's only. Anyone else gets 404 "Deck not found". Both stay on the default
// policy, so a pending person gets 403 pending_approval (SW-54 S3).
[Route("api/decks/{id}/sharing")]
public class SharingController(SharingService sharing) : ApiControllerBase
{
    // A level and up to the viewer cap of email addresses: 64 KB is far more than that, and far less than Kestrel's 30 MB.
    public const int MAX_BODY_BYTES = 64 * 1024;

    [HttpGet]
    [ProducesResponseType<SharingView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<SharingView> Get(string id) => sharing.GetAsync(id);

    [HttpPut]
    [SharingBodyErrors]
    [RequestBodyLimit(MAX_BODY_BYTES)]
    [ProducesResponseType<SharingView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<SharingView> Put(string id, SharingUpdate update) => sharing.UpdateAsync(id, update);
}
