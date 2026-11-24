using SlideWorks.WebApi.Features.Presenting.Views;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Tests.Live;

// IPresentations for the tests of Go live, which only need to know that a presentation was started: it records the decks
// StartAsync was called for. Everything else is not part of what those tests do.
public sealed class PresentationStartSpy : IPresentations
{
    private readonly Lock _gate = new();
    private readonly List<string> _started = [];

    public IReadOnlyList<string> Started
    {
        get
        {
            lock (_gate)
            {
                return [.. _started];
            }
        }
    }

    public Task<PresentationState> StartAsync(string deckId)
    {
        lock (_gate)
        {
            _started.Add(deckId);
        }
        return Task.FromResult<PresentationState>(null!);
    }

    public Task<PresentationJoined> JoinAsync(string connectionId, string deckId, string clientId, PresentationRole role, string label, string timeZone, PresentationResume? resume)
        => throw new NotSupportedException();

    public Task<PresentationState> UpdateAsync(string connectionId, string deckId, PresentationUpdate update) => throw new NotSupportedException();

    public Task<PresentationState> SetTimerAsync(string connectionId, string deckId, TimerCommand command) => throw new NotSupportedException();

    public Task LeaveAsync(string connectionId, string deckId) => throw new NotSupportedException();

    public Task LeaveAllAsync(string connectionId) => Task.CompletedTask;

    public Task<LivePresentationsView> ListLiveAsync(Func<string, Task<bool>> canPresent) => throw new NotSupportedException();

    public Task TalkLengthChangedAsync(string deckId, int? minutes) => throw new NotSupportedException();

    public Task<PresentationState?> GetAsync(string deckId) => Task.FromResult<PresentationState?>(null);
}
