using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support;

// ITemplateDrafts for route tests: answers the screen samples unless a test sets another answer, records its
// calls as "Method id" (the method without Async), in order, and throws what a test hands to Refuse. The app, and so this
// fake, is shared by every test in a fixture: a test that calls Refuse, or sets an answer, puts it back when it is done.
public sealed class FakeTemplateDrafts : ITemplateDrafts
{
    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];
    private Exception? _refusal;

    public TemplatesView Templates { get; set; } = TemplateScreenShapes.Sample<TemplatesView>(TemplateScreenShapes.TEMPLATES_FILE);

    public TemplateEditorView Editor { get; set; } = TemplateScreenShapes.Sample<TemplateEditorView>(TemplateScreenShapes.EDITOR_FILE);

    public TemplateCreated Created { get; set; } = new("tpl_Nw4kT8pQz2Lm");

    // The manifest JSON of the last UpdateAsync, as the service received it.
    public string? LastManifestJson { get; private set; }

    public static void Register(IServiceCollection services)
        => services.AddSingleton<FakeTemplateDrafts>().AddSingleton<ITemplateDrafts>(provider => provider.GetRequiredService<FakeTemplateDrafts>());

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

    public Task<TemplatesView> ListAsync(ICurrentActor actor) => Task.FromResult(Answer("List", Templates));

    public Task<TemplateCreated> CreateAsync(ICurrentActor actor, string basedOn, string name)
        => Task.FromResult(Answer($"Create {basedOn} {name}", Created));

    public Task<TemplateEditorView> GetAsync(ICurrentActor actor, string id) => Task.FromResult(Answer($"Get {id}", Editor));

    public Task<TemplateEditorView> UpdateAsync(ICurrentActor actor, string id, string manifestJson, int revision)
    {
        var answer = Answer($"Update {id} {revision}", Editor);
        LastManifestJson = manifestJson;
        return Task.FromResult(answer);
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
