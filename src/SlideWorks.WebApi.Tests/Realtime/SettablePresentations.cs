using SlideWorks.WebApi.Features.Presenting.Views;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Tests.Realtime;

// IPresentations for the audience relay's tests, standing in for SW-4's registry: a test sets the state a deck is in, and
// calls the observers itself (the registry's job) with Notify. StartAsync keeps the state a deck already has, or begins at
// slide 1, as the real one does; Drop forgets a deck's state, as a restart does. Nothing else is part of what these tests do.
public sealed class SettablePresentations : IPresentations
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, PresentationState> _states = new(StringComparer.Ordinal);

    public static PresentationState At(string deckId, int slide, int step = 0, bool black = false, long seq = 1)
        => new(deckId, "p-test", seq, slide, step, black, false, new PresentationTimer(TimerState.Running, 1_234_567), 45, []);

    public void Set(PresentationState state)
    {
        lock (_gate)
        {
            _states[state.DeckId] = state;
        }
    }

    public void Drop(string deckId)
    {
        lock (_gate)
        {
            _states.Remove(deckId);
        }
    }

    public Task<PresentationState?> GetAsync(string deckId)
    {
        lock (_gate)
        {
            return Task.FromResult(_states.GetValueOrDefault(deckId));
        }
    }

    public Task<PresentationState> StartAsync(string deckId)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(deckId, out var state))
            {
                state = At(deckId, 1);
                _states[deckId] = state;
            }
            return Task.FromResult(state);
        }
    }

    public Task<PresentationJoined> JoinAsync(string connectionId, string deckId, string clientId, PresentationRole role, string label, string timeZone, PresentationResume? resume)
        => throw new NotSupportedException();

    public Task<PresentationState> UpdateAsync(string connectionId, string deckId, PresentationUpdate update) => throw new NotSupportedException();

    public Task<PresentationState> SetTimerAsync(string connectionId, string deckId, TimerCommand command) => throw new NotSupportedException();

    public Task LeaveAsync(string connectionId, string deckId) => throw new NotSupportedException();

    public Task LeaveAllAsync(string connectionId) => Task.CompletedTask;

    public Task<LivePresentationsView> ListLiveAsync(Guid ownerId) => throw new NotSupportedException();

    public Task TalkLengthChangedAsync(string deckId, int? minutes) => throw new NotSupportedException();
}
