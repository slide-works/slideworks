namespace SlideWorks.WebApi.Features.OpenResponses;

// Publishes the word-cloud event for one slide. A singleton.
public interface IWordCloudPublisher
{
    Task PublishAsync(string deckId, int slide, CancellationToken cancellationToken);
}
