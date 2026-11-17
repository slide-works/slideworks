using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support;

// ITemplatePublishing for route tests: answers the screen samples unless a test sets another answer, records its
// calls as "Method id" (the method without Async), in order, and throws what a test hands to Refuse. The app, and so this
// fake, is shared by every test in a fixture: a test that calls Refuse, or sets an answer, puts it back when it is done.
public sealed class FakeTemplatePublishing : ITemplatePublishing
{
    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];
    private Exception? _refusal;

    public TemplateChecklistView Checklist { get; set; } = SampleChecklist();

    public TemplatePublished Published { get; set; } = new(3, 2);

    public LayoutReviewView Review { get; set; } = TemplateScreenShapes.Sample<LayoutReviewView>(TemplateScreenShapes.LAYOUT_REVIEW_FILE);

    public static void Register(IServiceCollection services)
        => services.AddSingleton<FakeTemplatePublishing>().AddSingleton<ITemplatePublishing>(provider => provider.GetRequiredService<FakeTemplatePublishing>());

    // Every call from now on throws error and records nothing; null accepts calls again.
    public void Refuse(Exception? error)
    {
        lock (_gate)
        {
            _refusal = error;
        }
    }

    // The calls answered so far, in order.
    public IReadOnlyList<string> Calls()
    {
        lock (_gate)
        {
            return [.. _calls];
        }
    }

    public Task<TemplateChecklistView> ChecklistAsync(ICurrentActor actor, string id) => Task.FromResult(Answer($"Checklist {id}", Checklist));

    public Task<TemplatePublished> PublishAsync(ICurrentActor actor, string id) => Task.FromResult(Answer($"Publish {id}", Published));

    public Task ArchiveAsync(ICurrentActor actor, string id) => Task.FromResult(Answer($"Archive {id}", true));

    public Task<LayoutReviewView> LayoutReviewAsync(ICurrentActor actor, string id, string name)
        => Task.FromResult(Answer($"LayoutReview {id} {name}", Review));

    // Records "ApproveLayout id name asset overflow=a,b contrast=c".
    public Task ApproveLayoutAsync(ICurrentActor actor, string id, string name, string asset, IReadOnlyList<string> overflow, IReadOnlyList<string> contrast)
        => Task.FromResult(Answer($"ApproveLayout {id} {name} {asset} overflow={string.Join(',', overflow)} contrast={string.Join(',', contrast)}", true));

    private static TemplateChecklistView SampleChecklist()
    {
        var editor = TemplateScreenShapes.Sample<TemplateEditorView>(TemplateScreenShapes.EDITOR_FILE);
        return new TemplateChecklistView(editor.Checklist, editor.CanPublish);
    }

    private T Answer<T>(string call, T answer)
    {
        lock (_gate)
        {
            if (_refusal != null)
            {
                throw _refusal;
            }
            _calls.Add(call);
            return answer;
        }
    }
}
