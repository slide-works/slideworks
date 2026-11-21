using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support;

// ITemplates for tests that need a template the built-ins do not have: the built-ins plus whatever a test adds with Add.
// An added template is answered before a built-in of the same ref, whatever version is asked, so a test can widen "talk".
// FindAsync with no version answers the highest version added (the last one added, among equals). ListForAsync leaves out an archived template and
// one the actor may not pick. Swap it in on a test host with FakeTemplates.Register. The app, and so this fake, is shared
// by every test in a fixture: each test adds refs of its own.
public sealed class FakeTemplates : ITemplates
{
    private readonly Lock _gate = new();
    private readonly TemplateCatalogue _builtIns = new();
    private readonly List<(ResolvedTemplate Template, IReadOnlyList<string> PickableBy)> _added = [];

    public static void Register(IServiceCollection services)
        => services.AddSingleton<FakeTemplates>().AddSingleton<ITemplates>(provider => provider.GetRequiredService<FakeTemplates>());

    // pickableBy is the e-mails of the people who may pick the template; none means everyone signed in.
    public void Add(ResolvedTemplate template, params string[] pickableBy)
    {
        lock (_gate)
        {
            _added.Add((template, pickableBy));
        }
    }

    // Forgets every template added, so the next test of the fixture sees the built-ins again.
    public void Reset()
    {
        lock (_gate)
        {
            _added.Clear();
        }
    }

    public Task<ResolvedTemplate?> FindAsync(string templateRef, int? version, CancellationToken ct)
    {
        lock (_gate)
        {
            var added = _added.Select(a => a.Template).Where(t => string.Equals(t.Ref, templateRef, StringComparison.Ordinal)).ToList();
            if (added.Count == 0)
            {
                return Task.FromResult(_builtIns.Find(templateRef) is { } builtIn ? BuiltInTemplates.Resolve(builtIn) : null);
            }
            // A built-in has no versions, so the version asked is not looked at for its ref, as with the real one.
            var anyVersion = version is null || _builtIns.Find(templateRef) is not null;
            return Task.FromResult(anyVersion ? Latest(added) : added.LastOrDefault(t => t.Version == version));
        }
    }

    public async Task<ResolvedTemplate> ForDeckAsync(Deck deck, CancellationToken ct)
        => await FindAsync(deck.Template, deck.TemplateVersion, ct) ?? BuiltInTemplates.Resolve(_builtIns.Default);

    public Task<IReadOnlyList<ResolvedTemplate>> ListForAsync(ICurrentActor actor, CancellationToken ct)
    {
        lock (_gate)
        {
            var refs = _added.Select(a => a.Template.Ref).Distinct(StringComparer.Ordinal).ToList();
            var added = refs
                .Select(r => _added.Where(a => string.Equals(a.Template.Ref, r, StringComparison.Ordinal)).MaxBy(a => a.Template.Version ?? 0))
                .Where(a => !a.Template.Archived && MayPick(actor, a.PickableBy))
                .Select(a => a.Template);
            var builtIns = _builtIns.All.Where(t => !refs.Contains(t.Name, StringComparer.Ordinal)).Select(BuiltInTemplates.Resolve);
            return Task.FromResult<IReadOnlyList<ResolvedTemplate>>([.. builtIns, .. added]);
        }
    }

    // MaxBy answers the first of equals, so the list is walked from the last one added.
    private static ResolvedTemplate? Latest(IEnumerable<ResolvedTemplate> templates) => templates.Reverse().MaxBy(t => t.Version ?? 0);

    private static bool MayPick(ICurrentActor actor, IReadOnlyList<string> pickableBy)
        => actor.IsSignedIn && (pickableBy.Count == 0 || pickableBy.Contains(actor.Email, StringComparer.OrdinalIgnoreCase));
}
