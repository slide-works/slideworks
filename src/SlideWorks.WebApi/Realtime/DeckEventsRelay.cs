using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.SignalR;

namespace SlideWorks.WebApi.Realtime;

// Subscribes to the bus for every deck and forwards each event to the hub connections that joined that deck. The bus
// delivers off the publishing request and in publish order, so a slow client never blocks a save.
[RegisterSingleton(For = typeof(IHostedService))]
public sealed class DeckEventsRelay(IDeckEvents events, IHubContext<DeckHub> hub) : IHostedService
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = events.Subscribe(null, ForwardAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }

    private Task ForwardAsync(DeckEvent deckEvent)
        => hub.Clients.Group(DeckHub.GroupName(deckEvent.DeckId)).SendAsync(DeckHub.EVENT_METHOD, Payload(deckEvent));

    // Serialised as DeckEvent so the "type" discriminator is written (SignalR would use the runtime type without it).
    private static JsonElement Payload(DeckEvent deckEvent) => JsonSerializer.SerializeToElement(deckEvent, JsonSerializerOptions.Web);
}
