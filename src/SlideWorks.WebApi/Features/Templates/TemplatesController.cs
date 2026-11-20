using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// The templates page, the template editor, publishing and the custom layout review. Signed out, the fallback policy
// answers 401 before this runs. A template the caller may not see is 404; a refusal is TemplateErrors' 400 or 409.
[Route("api/templates")]
[TemplateErrors]
public class TemplatesController(ITemplateDrafts drafts, ITemplatePublishing publishing, ICurrentActor actor) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<TemplatesView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public Task<TemplatesView> List() => drafts.ListAsync(actor);

    [HttpPost]
    [ProducesResponseType<TemplateCreated>(StatusCodes.Status201Created)]
    [ProducesResponseType<TemplateErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ObjectResult> Create(CreateTemplateRequest request)
        => StatusCode(StatusCodes.Status201Created, await drafts.CreateAsync(actor, request.BasedOn, request.Name));

    [HttpGet("{id}")]
    [ProducesResponseType<TemplateEditorView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<TemplateEditorView> Get(string id) => drafts.GetAsync(actor, id);

    [HttpPut("{id}")]
    [ProducesResponseType<TemplateEditorView>(StatusCodes.Status200OK)]
    [ProducesResponseType<TemplateErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<TemplateErrorResponse>(StatusCodes.Status409Conflict)]
    public Task<TemplateEditorView> Update(string id, UpdateTemplateRequest request)
        => drafts.UpdateAsync(actor, id, request.Manifest.GetRawText(), request.Revision);

    [HttpGet("{id}/checklist")]
    [ProducesResponseType<TemplateChecklistView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<TemplateChecklistView> Checklist(string id) => publishing.ChecklistAsync(actor, id);

    [HttpPost("{id}/publish")]
    [ProducesResponseType<TemplatePublished>(StatusCodes.Status200OK)]
    [ProducesResponseType<TemplateErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<TemplatePublished> Publish(string id) => publishing.PublishAsync(actor, id);

    [HttpPost("{id}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Archive(string id) => NoContentAfter(publishing.ArchiveAsync(actor, id));

    [HttpGet("{id}/layouts/{name}/review")]
    [ProducesResponseType<LayoutReviewView>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<LayoutReviewView> LayoutReview(string id, string name) => publishing.LayoutReviewAsync(actor, id, name);

    [HttpPost("{id}/layouts/{name}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<TemplateErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> ApproveLayout(string id, string name, ApproveLayoutRequest request)
        => NoContentAfter(publishing.ApproveLayoutAsync(actor, id, name, request.Asset, request.Overflow, request.Contrast));
}
