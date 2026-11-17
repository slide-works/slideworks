namespace SlideWorks.WebApi.Realtime;

// The in-process bus for audience events, like IDeckEvents (J4): a feature publishes and moves on, and the hub's relay
// forwards to audience:{deckId} (guests) or audience-watch:{deckId} (the owner). Features never call IHubContext.
public interface IAudienceEvents
{
    Task PublishAsync(AudienceEvent audienceEvent);

    IDisposable Subscribe(string? deckId, Func<AudienceEvent, Task> handler);
}
