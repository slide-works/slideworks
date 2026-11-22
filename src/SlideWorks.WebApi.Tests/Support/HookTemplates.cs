using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support;

// ITemplates that runs a test's hook once the next time a deck's template is resolved, then answers as FakeTemplates does.
// add_slides resolves it after its pre-checks' deck read and before its write transaction, so a test can commit another
// call in that gap. Arm it with Arm; the hook runs once.
public sealed class HookTemplates : ITemplates
{
    private readonly FakeTemplates _inner = new();
    private Func<Task>? _hook;

    public static void Register(IServiceCollection services)
        => services.AddSingleton<HookTemplates>().AddSingleton<ITemplates>(provider => provider.GetRequiredService<HookTemplates>());

    public void Arm(Func<Task> hook) => _hook = hook;

    public Task<ResolvedTemplate?> FindAsync(string templateRef, int? version, CancellationToken ct) => _inner.FindAsync(templateRef, version, ct);

    public async Task<ResolvedTemplate> ForDeckAsync(Deck deck, CancellationToken ct)
    {
        var hook = Interlocked.Exchange(ref _hook, null);
        if (hook is not null)
        {
            await hook();
        }
        return await _inner.ForDeckAsync(deck, ct);
    }

    public Task<IReadOnlyList<ResolvedTemplate>> ListForAsync(ICurrentActor actor, CancellationToken ct) => _inner.ListForAsync(actor, ct);
}
