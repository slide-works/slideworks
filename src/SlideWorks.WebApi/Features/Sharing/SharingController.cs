using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Sharing.Views;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Sharing;

// The sharing dialog (K4 sharing.json): the owner's only. Anyone else gets 404 "Deck not found". Both stay on the default
// policy, so a pending person gets 403 pending_approval (SW-54 S3).
[Route("api/decks/{id}/sharing")]
public class SharingController(SharingService sharing) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<SharingView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<SharingView> Get(string id) => sharing.GetAsync(id);

    [HttpPut]
    [SharingBodyErrors]
    [ProducesResponseType<SharingView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<SharingView> Put(string id, SharingUpdate update) => sharing.UpdateAsync(id, update);
}
