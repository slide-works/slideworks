namespace SlideWorks.WebApi.Realtime;

// The in-process event bus. Publishers call PublishAsync and move on: delivery happens off the publishing request, in
// publish order per subscriber, so a slow handler never blocks a save. Subscribe(deckId, handler) delivers one deck's
// events (null = every deck) until the returned IDisposable is disposed; a handler that throws is logged and the
// subscription carries on. DeckEventsRelay subscribes to every deck and forwards to the browsers on DeckHub.
//
// Single process only: SlideWorks runs as one container on SQLite. Running more than one instance needs a bridge
// between processes here (Codex uses Postgres LISTEN/NOTIFY) and a SignalR backplane (ShipLean uses Redis).
public interface IDeckEvents
{
    Task PublishAsync(DeckEvent deckEvent);

    IDisposable Subscribe(Guid? deckId, Func<DeckEvent, Task> handler);
}
