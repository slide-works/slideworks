using Microsoft.AspNetCore.Mvc;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// A template as a .zip bundle (D4): upload one to make a draft, download a template's own. Both stay on the default
// policy, so a signed-out caller gets 401 and a pending person 403 pending_approval.
[Route("api/templates")]
[TemplateErrors]
public class TemplateBundleController(ITemplateBundles bundles, ICurrentActor actor) : ApiControllerBase
{
    private const string ZIP = BundleBodyAttribute.ZIP;

    // The zip is the request body itself, not a form field. Kestrel's default body limit is below a full bundle, so the
    // action carries its own; a body over it is 413, and one that is not a zip 415 (BundleBody). S5693 wants a body of at most 8 MB: a bundle holds
    // fonts and art of up to 60 MB (D65), it is read as a stream and never held in memory, and only an approved person
    // reaches this action.
    [HttpPost("import")]
#pragma warning disable S5693
    [RequestSizeLimit(TemplateLimits.MAX_BUNDLE_BYTES)]
#pragma warning restore S5693
    [BundleBody]
    [ProducesResponseType<TemplateImported>(StatusCodes.Status201Created)]
    [ProducesResponseType<TemplateErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<TemplateErrorResponse>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ObjectResult> Import()
        => StatusCode(StatusCodes.Status201Created, new TemplateImported(await bundles.ImportAsync(actor, Request.Body, HttpContext.RequestAborted)));

    // The owner's only.
    [HttpGet("{id}/bundle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<FileStreamResult> Export(string id) => Attachment(await bundles.ExportAsync(actor, id, HttpContext.RequestAborted));

    private FileStreamResult Attachment(TemplateBundleFile file) => File(file.Content, ZIP, file.FileName);
}
