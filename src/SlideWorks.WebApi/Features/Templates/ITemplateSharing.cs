using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// Who may use a template (T5). GetAsync and SetAsync are the owner's only. A template the actor may not see is
// NotFoundException.
public interface ITemplateSharing
{
    Task<TemplateSharingView> GetAsync(ICurrentActor actor, string id);

    Task<TemplateSharingView> SetAsync(ICurrentActor actor, string id, TemplateSharingUpdate update);

    // The template's page at /t/{id}. The actor may be signed out (ICurrentActor.IsSignedIn), which is enough when the
    // template is shared by link.
    Task<TemplatePublicView> PublicAsync(ICurrentActor actor, string id);

    // A signed-in person adds a template shared by link to their "Shared with you", once.
    Task UseAsync(ICurrentActor actor, string id);
}
