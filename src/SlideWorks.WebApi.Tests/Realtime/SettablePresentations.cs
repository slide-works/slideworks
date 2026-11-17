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
    private int _startCalls;
    private (TaskCompletionSource Entered, TaskCompletionSource Release)? _held;
    private TaskCompletionSource? _release;

    // How many times anything asked this registry to start a presentation.
    public int StartCalls => Volatile.Read(ref _startCalls);

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

    // Arms the next GetAsync to stop before it answers. The returned task completes when that call has arrived (so whoever
    // made it is holding whatever it held), and it stays there until ReleaseGet. Only one call is held, once.
    public Task BlockNextGet()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _held = (entered, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            _release = null;
        }
        return entered.Task;
    }

    public void ReleaseGet()
    {
        lock (_gate)
        {
            _release?.TrySetResult();
        }
    }

    // Runs once inside the next GetAsync, after it has read the state and before it answers: the registry changing under a read.
    public void AfterNextGet(Func<string, Task>? hook) => Interlocked.Exchange(ref _afterGet, hook);

    private Func<string, Task>? _afterGet;

    public async Task<PresentationState?> GetAsync(string deckId)
    {
        (TaskCompletionSource Entered, TaskCompletionSource Release)? held;
        lock (_gate)
        {
            held = _held;
            _held = null;
            _release = held?.Release;
        }
        if (held is { } pair)
        {
            pair.Entered.TrySetResult();
            await pair.Release.Task;
        }
        PresentationState? read;
        lock (_gate)
        {
            read = _states.GetValueOrDefault(deckId);
        }
        var after = Interlocked.Exchange(ref _afterGet, null);
        if (after != null)
        {
            await after(deckId);
        }
        return read;
    }

    public Task<PresentationState> StartAsync(string deckId)
    {
        Interlocked.Increment(ref _startCalls);
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

    // A test sets it to make LeaveAllAsync fail, as a registry that throws on a disconnect would.
    public bool ThrowOnLeaveAll { get; set; }

    public Task LeaveAllAsync(string connectionId) => ThrowOnLeaveAll ? throw new InvalidOperationException("the registry failed") : Task.CompletedTask;

    public Task<LivePresentationsView> ListLiveAsync(Guid ownerId) => throw new NotSupportedException();

    public Task TalkLengthChangedAsync(string deckId, int? minutes) => throw new NotSupportedException();
}
