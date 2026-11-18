using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support;

// ITemplateSharing for route tests: answers the screen samples unless a test sets another answer, records its
// calls as "Method id" (the method without Async), in order (PublicAsync adds whether the actor is signed in), and throws what a test hands to Refuse. The app, and so this
// fake, is shared by every test in a fixture: a test that calls Refuse, or sets an answer, puts it back when it is done.
public sealed class FakeTemplateSharing : ITemplateSharing
{
    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];
    private Exception? _refusal;

    public TemplateSharingView Sharing { get; set; } = TemplateScreenShapes.Sample<TemplateSharingView>(TemplateScreenShapes.SHARING_FILE);

    public TemplatePublicView Page { get; set; } = TemplateScreenShapes.Sample<TemplatePublicView>(TemplateScreenShapes.PUBLIC_FILE);

    public static void Register(IServiceCollection services)
        => services.AddSingleton<FakeTemplateSharing>().AddSingleton<ITemplateSharing>(provider => provider.GetRequiredService<FakeTemplateSharing>());

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

    public Task<TemplateSharingView> GetAsync(ICurrentActor actor, string id) => Task.FromResult(Answer($"Get {id}", Sharing));

    // Records "Set id level a@x,b@y".
    public Task<TemplateSharingView> SetAsync(ICurrentActor actor, string id, TemplateSharingUpdate update)
        => Task.FromResult(Answer($"Set {id} {update.Level} {string.Join(',', update.Viewers ?? [])}", Sharing));

    // Records "Public id signedIn" or "Public id signedOut".
    public Task<TemplatePublicView> PublicAsync(ICurrentActor actor, string id)
        => Task.FromResult(Answer($"Public {id} {(actor.IsSignedIn ? "signedIn" : "signedOut")}", Page));

    public Task UseAsync(ICurrentActor actor, string id) => Task.FromResult(Answer($"Use {id}", true));

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
