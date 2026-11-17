using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// Who may use a template: the owner's sharing dialog, the template's page at /t/{id}, and "Use this template". The
// page is open to anonymous callers; ITemplateSharing decides, and a template the caller may not see is 404.
[Route("api/templates/{id}")]
[TemplateErrors]
public class TemplateSharingController(ITemplateSharing sharing, ICurrentActor actor) : ApiControllerBase
{
    [HttpGet("sharing")]
    [ProducesResponseType<TemplateSharingView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<TemplateSharingView> Get(string id) => sharing.GetAsync(actor, id);

    [HttpPut("sharing")]
    [ProducesResponseType<TemplateSharingView>(StatusCodes.Status200OK)]
    [ProducesResponseType<TemplateErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<TemplateSharingView> Put(string id, TemplateSharingUpdate update) => sharing.SetAsync(actor, id, update);

    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType<TemplatePublicView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<TemplatePublicView> Public(string id) => sharing.PublicAsync(actor, id);

    [HttpPost("use")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Use(string id) => NoContentAfter(sharing.UseAsync(actor, id));
}
