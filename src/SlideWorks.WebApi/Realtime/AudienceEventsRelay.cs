using System.Text.Json;
using System.Text.Json.Serialization;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.SignalR;

namespace SlideWorks.WebApi.Realtime;

// Subscribes to the audience bus for every deck and forwards each event over the hub (J4): audienceState and
// audienceEnded to audience:{deckId} (guests), joinedCount to audience-watch:{deckId} (the owner). All go on the method
// "audienceEvent". A joinedCount goes at most once a second per deck: the first goes at once, and a change inside the
// second waits for it to pass and sends the latest count then, so the owner always ends on the true number.
[RegisterSingleton(For = typeof(IHostedService))]
public sealed class AudienceEventsRelay(IAudienceEvents events, IHubContext<DeckHub> hub, TimeProvider time, ILogger<AudienceEventsRelay> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan _countInterval = TimeSpan.FromSeconds(1);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, CountWindow> _windows = new(StringComparer.Ordinal);
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = events.Subscribe(null, ForwardAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        lock (_gate)
        {
            foreach (var window in _windows.Values)
            {
                window.Timer?.Dispose();
            }
            _windows.Clear();
        }
    }

    private Task ForwardAsync(AudienceEvent audienceEvent) => audienceEvent switch
    {
        JoinedCount count => SendCountAsync(count),
        _ => SendAsync(DeckHub.AudienceGroupName(audienceEvent.DeckId), audienceEvent),
    };

    private Task SendAsync(string group, AudienceEvent audienceEvent)
        => hub.Clients.Group(group).SendAsync(DeckHub.AUDIENCE_EVENT_METHOD, Payload(audienceEvent));

    private Task SendCountAsync(JoinedCount count)
    {
        lock (_gate)
        {
            var window = _windows.GetValueOrDefault(count.DeckId);
            if (window == null || time.GetUtcNow() - window.SentAt >= _countInterval)
            {
                _windows[count.DeckId] = new CountWindow(time.GetUtcNow(), null, null);
                return SendAsync(DeckHub.AudienceWatchGroupName(count.DeckId), count);
            }
            window.Waiting = count;
            window.Timer ??= time.CreateTimer(state => _ = FlushAsync((string)state!), count.DeckId, window.SentAt + _countInterval - time.GetUtcNow(), Timeout.InfiniteTimeSpan);
            return Task.CompletedTask;
        }
    }

    private async Task FlushAsync(string deckId)
    {
        JoinedCount? waiting;
        lock (_gate)
        {
            if (!_windows.TryGetValue(deckId, out var window))
            {
                return;
            }
            waiting = window.Waiting;
            window.Timer?.Dispose();
            _windows[deckId] = new CountWindow(time.GetUtcNow(), null, null);
        }
        if (waiting != null)
        {
            try
            {
                await SendAsync(DeckHub.AudienceWatchGroupName(deckId), waiting);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Sending the joined count for deck {DeckId} failed", deckId);
            }
        }
    }

    // Serialised as AudienceEvent so the "type" discriminator is written, with enums as camelCase strings like the hub's
    // own protocol.
    private static JsonElement Payload(AudienceEvent audienceEvent)
        => JsonSerializer.SerializeToElement(audienceEvent, _options);

    private static readonly JsonSerializerOptions _options = new(JsonSerializerOptions.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private sealed class CountWindow(DateTimeOffset sentAt, JoinedCount? waiting, ITimer? timer)
    {
        public DateTimeOffset SentAt { get; } = sentAt;

        public JoinedCount? Waiting { get; set; } = waiting;

        public ITimer? Timer { get; set; } = timer;
    }
}
