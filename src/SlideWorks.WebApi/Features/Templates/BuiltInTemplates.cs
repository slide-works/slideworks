using SlideWorks.Core.Look;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// ITemplates over the built-in catalogue (T4): every built-in can be found and picked by anyone, none has a version, an
// owner or a palette of its own.
// Not registered: Templates composes it with the uploaded ones.
public sealed class BuiltInTemplates(ITemplateCatalogue catalogue) : ITemplates
{
    public static ResolvedTemplate Resolve(Template template)
        => new(template, template.Name, null, true) { Look = TemplateLook.OfBuiltIn(template) };

    public Task<ResolvedTemplate?> FindAsync(string templateRef, int? version, CancellationToken ct)
        => Task.FromResult(catalogue.Find(templateRef) is { } template ? Resolve(template) : null);

    public async Task<ResolvedTemplate> ForDeckAsync(Deck deck, CancellationToken ct)
        => await FindAsync(deck.Template, deck.TemplateVersion, ct) ?? Resolve(catalogue.Default);

    public Task<IReadOnlyList<ResolvedTemplate>> ListForAsync(ICurrentActor actor, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ResolvedTemplate>>([.. catalogue.All.Select(Resolve)]);
}
