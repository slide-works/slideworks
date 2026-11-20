using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// Publishing a template and reviewing its custom layouts (T5). Every method is the owner's only: anyone else gets
// NotFoundException. A refused action is TemplateRefusedException and changes nothing.
public interface ITemplatePublishing
{
    Task<TemplateChecklistView> ChecklistAsync(ICurrentActor actor, string id);

    // Publishes the draft as the next version.
    Task<TemplatePublished> PublishAsync(ICurrentActor actor, string id);

    // Hides the template from the pickers; decks on it keep rendering.
    Task ArchiveAsync(ICurrentActor actor, string id);

    Task<LayoutReviewView> LayoutReviewAsync(ICurrentActor actor, string id, string name);

    // Approves the layout file asset for the custom layout name, accepting the listed overflow and contrast findings.
    Task ApproveLayoutAsync(ICurrentActor actor, string id, string name, string asset, IReadOnlyList<string> overflow, IReadOnlyList<string> contrast);
}
