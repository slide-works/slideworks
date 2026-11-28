using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Tests.Support;

// IAudienceRelay for tests that need a position without a presentation: each test sets the state a deck answers with. It
// records the decks Go live started, End ended and a refresh asked for, in order, and the language each read asked for. Add
// it with FakeAudienceRelay.AddTo. A deck with no state set answers null, as the real relay does for a deck with no session.
// Like the stand-in relay, it answers the same state whatever the language.
public sealed class FakeAudienceRelay : IAudienceRelay
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AudienceState> _states = new(StringComparer.Ordinal);
    private readonly List<string> _started = [];
    private readonly List<string> _ended = [];
    private readonly List<string> _refreshed = [];
    private readonly List<(string DeckId, string? Language)> _reads = [];

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

    public IReadOnlyList<string> Refreshed
    {
        get
        {
            lock (_gate)
            {
                return [.. _refreshed];
            }
        }
    }

    // The refreshes asked for this deck, as a count: each test uses a deck of its own, as with FakePollPublisher.PublishedFor.
    public int RefreshedFor(string deckId)
    {
        lock (_gate)
        {
            return _refreshed.Count(d => d == deckId);
        }
    }

    // The language each CurrentAsync for this deck asked for, in order (null when none was given).
    public IReadOnlyList<string?> LanguagesReadFor(string deckId)
    {
        lock (_gate)
        {
            return [.. _reads.Where(r => r.DeckId == deckId).Select(r => r.Language)];
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

    public Task<AudienceState?> CurrentAsync(string deckId, string? language)
    {
        lock (_gate)
        {
            _reads.Add((deckId, language));
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

    public Task RefreshAsync(string deckId)
    {
        lock (_gate)
        {
            _refreshed.Add(deckId);
        }
        return Task.CompletedTask;
    }
}
