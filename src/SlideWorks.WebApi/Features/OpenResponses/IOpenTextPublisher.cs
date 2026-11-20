namespace SlideWorks.WebApi.Features.OpenResponses;

// Publishes the open-text event for one slide. A singleton.
public interface IOpenTextPublisher
{
    Task PublishAsync(string deckId, int slide, CancellationToken cancellationToken);
}
