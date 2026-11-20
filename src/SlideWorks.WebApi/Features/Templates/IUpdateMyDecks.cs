using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// "Update my decks" (D14): after a template's owner publishes a new version, moves every deck that person owns on the
// template to it. Other people's decks are left for their owners.
public interface IUpdateMyDecks
{
    // Returns how many decks moved; 0 when all of them were already on the latest version.
    Task<int> RunAsync(ICurrentActor actor, string templateId, CancellationToken ct);
}
