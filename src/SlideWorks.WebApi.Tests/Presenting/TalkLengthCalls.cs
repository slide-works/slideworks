using System.Collections.Concurrent;
using SlideWorks.WebApi.Features.Presenting.Views;
using SlideWorks.WebApi.Realtime.Presenting;

namespace SlideWorks.WebApi.Tests.Presenting;

// A fake IPresentations a fixture registers with ConfigureTestServices: it keeps every TalkLengthChangedAsync call and
// refuses the rest of the registry, which the talk-length tests never reach.
public sealed class TalkLengthCalls : IPresentations
{
    private readonly ConcurrentQueue<(string DeckId, int? Minutes)> _calls = new();

    // The talk lengths the deck was told, in order. One fixture shares one registry across its tests, so ask per deck.
    public IReadOnlyList<int?> For(string deckId) => [.. _calls.Where(call => call.DeckId == deckId).Select(call => call.Minutes)];

    public Task TalkLengthChangedAsync(string deckId, int? minutes)
    {
        _calls.Enqueue((deckId, minutes));
        return Task.CompletedTask;
    }

    public Task<PresentationJoined> JoinAsync(string connectionId, string deckId, string clientId, PresentationRole role, string label, string timeZone, PresentationResume? resume)
        => throw new NotSupportedException();

    public Task<PresentationState> UpdateAsync(string connectionId, string deckId, PresentationUpdate update) => throw new NotSupportedException();

    public Task<PresentationState> SetTimerAsync(string connectionId, string deckId, TimerCommand command) => throw new NotSupportedException();

    public Task LeaveAsync(string connectionId, string deckId) => Task.CompletedTask;

    public Task LeaveAllAsync(string connectionId) => Task.CompletedTask;

    public Task<LivePresentationsView> ListLiveAsync(Func<string, Task<bool>> canPresent) => throw new NotSupportedException();

    public Task<PresentationState?> GetAsync(string deckId) => throw new NotSupportedException();

    public Task<PresentationState> StartAsync(string deckId) => throw new NotSupportedException();
}
