using Agoda.IoC.Core;
using Microsoft.AspNetCore.SignalR;
using SlideWorks.WebApi.Features.Presenting.Views;

namespace SlideWorks.WebApi.Realtime.Presenting;

// TODO: replaced by the real presentations registry. It answers every call with one fixed state (slide 1, step 0, timer
// notStarted, the caller as the only connection), pushes it to the caller only, keeps no groups or state, lists no live
// presentations, reports clockMinutes 0 and ignores talk-length changes.
[RegisterSingleton(For = typeof(IPresentations))]
public sealed class StubPresentations(IHubContext<DeckHub> hub) : IPresentations
{
    private const string EPOCH = "p-stub";

    public async Task<PresentationJoined> JoinAsync(
        string connectionId,
        string deckId,
        string clientId,
        PresentationRole role,
        string label,
        string timeZone,
        PresentationResume? resume)
    {
        var state = Fixed(deckId, [new PresentationConnection(clientId, role, label)]);
        await PushAsync(connectionId, state);
        return new PresentationJoined(state, 0);
    }

    public async Task<PresentationState> UpdateAsync(string connectionId, string deckId, PresentationUpdate update)
    {
        var state = Fixed(deckId, []);
        await PushAsync(connectionId, state);
        return state;
    }

    public async Task<PresentationState> SetTimerAsync(string connectionId, string deckId, TimerCommand command)
    {
        var state = Fixed(deckId, []);
        await PushAsync(connectionId, state);
        return state;
    }

    public Task LeaveAsync(string connectionId, string deckId) => Task.CompletedTask;

    public Task LeaveAllAsync(string connectionId) => Task.CompletedTask;

    public Task<LivePresentationsView> ListLiveAsync(Guid ownerId) => Task.FromResult(new LivePresentationsView([]));

    public Task TalkLengthChangedAsync(string deckId, int? minutes) => Task.CompletedTask;

    public Task<PresentationState?> GetAsync(string deckId) => Task.FromResult<PresentationState?>(null);

    public Task<PresentationState> StartAsync(string deckId) => Task.FromResult(Fixed(deckId, []));

    private static PresentationState Fixed(string deckId, IReadOnlyList<PresentationConnection> connected)
        => new(deckId, EPOCH, 1, 1, 0, false, false, new PresentationTimer(TimerState.NotStarted, 0), null, connected);

    private Task PushAsync(string connectionId, PresentationState state)
        => hub.Clients.Client(connectionId).SendAsync(DeckHub.PRESENTATION_STATE_METHOD, state);
}
