using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// The one way to find a template (T4), built-in or uploaded. Every caller outside this folder asks here and never the
// Core catalogue, so an uploaded template reaches each of them at once.
public interface ITemplates
{
    // Any template, whoever asks. A null version is the latest published one; a built-in has no versions, so the version
    // is not looked at for one. Null when there is no such template or version.
    Task<ResolvedTemplate?> FindAsync(string templateRef, int? version, CancellationToken ct);

    // The deck's template at the version it stores. Never null: an unknown ref falls back to the default built-in.
    Task<ResolvedTemplate> ForDeckAsync(Deck deck, CancellationToken ct);

    // What the caller may pick, each at its latest version.
    Task<IReadOnlyList<ResolvedTemplate>> ListForAsync(ICurrentActor actor, CancellationToken ct);
}
