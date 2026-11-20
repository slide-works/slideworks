using Agoda.IoC.Core;
using SlideWorks.Core.OpenResponses;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// Publishes one fixed word-cloud event for a slide, already revealed, to the audience.
[RegisterSingleton(For = typeof(IWordCloudPublisher))]
public sealed class WordCloudPublisher(IAudienceEvents events) : IWordCloudPublisher
{
    public Task PublishAsync(string deckId, int slide, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // TODO: reading the rows replaces this fixed event.
        return events.PublishAsync(new WordCloudTally(
            deckId,
            OpenResponseTarget.Audience,
            slide,
            OpenResponsePhase.Open,
            "6e2f9a14-3c7b-4d58-a1e0-9b4c7d2f5a63",
            1,
            9,
            true,
            12,
            [
                new CloudWord("Focused", 5, false),
                new CloudWord("Busy", 4, false),
                new CloudWord("Calm", 2, false),
            ]));
    }
}
