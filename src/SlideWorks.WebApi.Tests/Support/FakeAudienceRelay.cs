using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Support;

// IAudienceRelay for tests that need a position without a presentation: each test sets the state a deck answers with. It
// records the decks Go live started and End ended, in order. Add it with FakeAudienceRelay.AddTo. A deck with no state set
// answers null, as the real relay does for a deck with no session.
public sealed class FakeAudienceRelay : IAudienceRelay
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AudienceState> _states = new(StringComparer.Ordinal);
    private readonly List<string> _started = [];
    private readonly List<string> _ended = [];

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

    public IReadOnlyList<string> Ended
    {
        get
        {
            lock (_gate)
            {
                return [.. _ended];
            }
        }
    }

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeAudienceRelay>().AddSingleton<IAudienceRelay>(provider => provider.GetRequiredService<FakeAudienceRelay>());

    public void Set(AudienceState state)
    {
        lock (_gate)
        {
            _states[state.DeckId] = state;
        }
    }

    public Task<AudienceState?> CurrentAsync(string deckId)
    {
        lock (_gate)
        {
            return Task.FromResult(_states.GetValueOrDefault(deckId));
        }
    }

    public Task StartedAsync(string deckId)
    {
        lock (_gate)
        {
            _started.Add(deckId);
        }
        return Task.CompletedTask;
    }

    public Task EndedAsync(string deckId)
    {
        lock (_gate)
        {
            _ended.Add(deckId);
        }
        return Task.CompletedTask;
    }
}
