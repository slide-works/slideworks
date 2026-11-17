using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// A person's template drafts (T5): the templates page and the editor. A template the actor may not see is
// NotFoundException; a refused change is TemplateRefusedException and changes nothing.
public interface ITemplateDrafts
{
    // The actor's own templates, the ones shared with them, and the built-in ones.
    Task<TemplatesView> ListAsync(ICurrentActor actor);

    // A new draft that starts from the template basedOn, with name as its label.
    Task<TemplateCreated> CreateAsync(ICurrentActor actor, string basedOn, string name);

    // The editor view of the actor's own template.
    Task<TemplateEditorView> GetAsync(ICurrentActor actor, string id);

    // Replaces the draft's manifest. A revision other than the draft's is refused with stale_revision.
    Task<TemplateEditorView> UpdateAsync(ICurrentActor actor, string id, string manifestJson, int revision);
}
