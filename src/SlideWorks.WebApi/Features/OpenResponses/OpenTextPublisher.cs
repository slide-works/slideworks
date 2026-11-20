using Agoda.IoC.Core;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// Publishes the open-text slide's round as it stands: the audience copy with hidden answers left out, and the presenter
// copy with every answer. A newly opened round is already revealed and already includes its answers (D14). A singleton,
// so the rows are read through a scope.
[RegisterSingleton(For = typeof(IOpenTextPublisher))]
public sealed class OpenTextPublisher(IServiceScopeFactory scopes, IAudienceEvents events) : IOpenTextPublisher
{
    public async Task PublishAsync(string deckId, int slide, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopes.CreateAsyncScope();
        if (await scope.ServiceProvider.GetRequiredService<OpenAnswers>().TalliesAsync(deckId, slide, cancellationToken) is not { } tallies)
        {
            return;
        }
        await events.PublishAsync(tallies.Audience);
        await events.PublishAsync(tallies.Presenter);
    }
}
