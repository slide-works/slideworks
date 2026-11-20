using Agoda.IoC.Core;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// Publishes one fixed open-text event for a slide, already revealed, to the audience.
[RegisterSingleton(For = typeof(IOpenTextPublisher))]
public sealed class OpenTextPublisher(IAudienceEvents events) : IOpenTextPublisher
{
    public Task PublishAsync(string deckId, int slide, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // TODO: reading the rows replaces this fixed event.
        return events.PublishAsync(new OpenTextTally(
            deckId,
            OpenResponseTarget.Audience,
            slide,
            OpenResponsePhase.Open,
            "a3d81c5e-7f2b-4e96-b0c4-1d8e6f9a2b57",
            1,
            6,
            true,
            4,
            [
                new WallAnswer(1, "Smaller stories, so review is quick", false),
                new WallAnswer(2, "A shared definition of done", false),
                new WallAnswer(4, "Pairing on the risky changes", false),
            ]));
    }
}
