using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.OpenResponses;

// Publishes a word-cloud slide's round as WordCloudReader reads it: the presenter's event with every word, then the
// audience's with the hidden words left out (D4, D6). A slide the reader returns null for publishes nothing, and a read
// that throws publishes nothing either. A singleton: the reader is per request, so it is resolved in a scope of its own
// on each call.
[RegisterSingleton(For = typeof(IWordCloudPublisher))]
public sealed class WordCloudPublisher(IServiceScopeFactory scopes, IAudienceEvents events) : IWordCloudPublisher
{
    public async Task PublishAsync(string deckId, int slide, CancellationToken cancellationToken)
    {
        WordCloudTally? tally;
        await using (var scope = scopes.CreateAsyncScope())
        {
            tally = await scope.ServiceProvider.GetRequiredService<WordCloudReader>().ReadAsync(deckId, slide, cancellationToken);
        }
        if (tally is null)
        {
            return;
        }
        await events.PublishAsync(tally);
        await events.PublishAsync(tally with { Target = OpenResponseTarget.Audience, Words = [.. tally.Words.Where(w => !w.Hidden)] });
    }
}
