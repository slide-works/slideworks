using System.Text.Json;
using System.Text.Json.Serialization;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.SignalR;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Realtime;

// Subscribes to the audience bus for every deck and forwards each event over the hub (J4, V5): audienceState and
// audienceEnded to audience:{deckId} (guests), joinedCount to audience-watch:{deckId} (the owner). A poll's events are
// routed so nothing a guest may not see reaches a guest: pollControl, which carries counts and the correct answer, goes to
// audience-watch:{deckId} only; pollTally goes by its Target, Audience to audience:{deckId} and Results to
// poll-results:{roundId}, which only DeckHub.WatchPoll joins. SW-9's events (Q5, Q7) likewise: questionTally, which holds
// only questions another guest may see, goes to audience:{deckId}; questionControl, which holds every question, and
// reactions, which never reach a phone, go to audience-watch:{deckId} only. The relay routes and nothing more: what each
// event holds and how often it goes are its publisher's. An event of any other type has no route and is not sent.
// All go on the method "audienceEvent". A joinedCount goes at most once a second per deck: the first goes at once, and a
// change inside the second waits for it to pass and sends the latest count then, so the owner always ends on the true number.
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
        AudienceState or AudienceEnded => SendAsync(DeckHub.AudienceGroupName(audienceEvent.DeckId), audienceEvent),
        PollControl control => SendAsync(DeckHub.AudienceWatchGroupName(control.DeckId), control),
        PollTally tally => SendAsync(TallyGroup(tally), tally),
        QuestionTally tally => SendAsync(DeckHub.AudienceGroupName(tally.DeckId), tally),
        QuestionControl control => SendAsync(DeckHub.AudienceWatchGroupName(control.DeckId), control),
        ReactionsBurst burst => SendAsync(DeckHub.AudienceWatchGroupName(burst.DeckId), burst),
        WordCloudTally tally => SendAsync(OpenResponseGroup(tally.DeckId, tally.Target), tally),
        OpenTextTally tally => SendAsync(OpenResponseGroup(tally.DeckId, tally.Target), tally),
        _ => throw new InvalidOperationException($"No hub group for the audience event {audienceEvent.GetType().Name}"),
    };

    // A tally's Target names its group; a Target with no group is never sent anywhere, rather than to the guests.
    private static string TallyGroup(PollTally tally) => tally.Target switch
    {
        PollTallyTarget.Audience => DeckHub.AudienceGroupName(tally.DeckId),
        PollTallyTarget.Results => DeckHub.PollResultsGroupName(tally.Round),
        _ => throw new InvalidOperationException($"No hub group for the poll tally target {tally.Target}"),
    };

    // A word cloud's or open-text slide's Target names its group: the audience copy to the guests and the big screen, the
    // presenter copy, hidden entries included, to the presenter's screens.
    private static string OpenResponseGroup(string deckId, OpenResponseTarget target) => target switch
    {
        OpenResponseTarget.Audience => DeckHub.AudienceGroupName(deckId),
        OpenResponseTarget.Presenter => DeckHub.AudienceWatchGroupName(deckId),
        _ => throw new InvalidOperationException($"No hub group for the open-response target {target}"),
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
                window?.Timer?.Dispose();
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
