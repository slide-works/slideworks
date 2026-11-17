using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// TODO: returns the screen sample until the real service lands.
[RegisterPerRequest(For = typeof(ITemplatePublishing))]
public sealed class StubTemplatePublishing : ITemplatePublishing
{
    // The editor sample's checklist and canPublish.
    public Task<TemplateChecklistView> ChecklistAsync(ICurrentActor actor, string id)
    {
        var editor = TemplateSamples.Read<TemplateEditorView>(TemplateSamples.EDITOR);
        return Task.FromResult(new TemplateChecklistView(editor.Checklist, editor.CanPublish));
    }

    public Task<TemplatePublished> PublishAsync(ICurrentActor actor, string id) => Task.FromResult(new TemplatePublished(3, 2));

    public Task ArchiveAsync(ICurrentActor actor, string id) => Task.CompletedTask;

    public Task<LayoutReviewView> LayoutReviewAsync(ICurrentActor actor, string id, string name) => Task.FromResult(TemplateSamples.Read<LayoutReviewView>(TemplateSamples.LAYOUT_REVIEW));

    public Task ApproveLayoutAsync(ICurrentActor actor, string id, string name, string asset, IReadOnlyList<string> overflow, IReadOnlyList<string> contrast)
        => Task.CompletedTask;
}
