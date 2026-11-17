using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// A template as a .zip bundle (D4): upload one to make a draft, download a template's own. Both stay on the default
// policy, so a signed-out caller gets 401 and a pending person 403 pending_approval.
[Route("api/templates")]
public class TemplateBundleController(ITemplateBundles bundles, ICurrentActor actor) : ApiControllerBase
{
    private const string ZIP = "application/zip";

    // The zip is the request body itself, not a form field.
    [HttpPost("import")]
    [Consumes(ZIP)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ObjectResult> Import()
        => StatusCode(StatusCodes.Status201Created, new { id = await bundles.ImportAsync(actor, Request.Body, HttpContext.RequestAborted) });

    // The owner's only.
    [HttpGet("{id}/bundle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<FileStreamResult> Export(string id) => Attachment(await bundles.ExportAsync(actor, id, HttpContext.RequestAborted));

    private FileStreamResult Attachment(TemplateBundleFile file) => File(file.Content, ZIP, file.FileName);
}
