using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Qa;

namespace SlideWorks.WebApi.Tests.Support;

// IQaRounds for tests of what opens and closes the Q&A round: it writes nothing and records each call as "Open {session}",
// "Close {session}" or "Ensure", per deck, in order. Add it with FakeQaRounds.AddTo. The app, and so this fake, is shared by
// every test in a fixture: each test uses deck ids of its own, or calls Reset when it is done.
public sealed class FakeQaRounds : IQaRounds
{
    private readonly Lock _gate = new();
    private readonly List<(string Deck, string Call)> _calls = [];

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeQaRounds>().AddSingleton<IQaRounds>(provider => provider.GetRequiredService<FakeQaRounds>());

    public IReadOnlyList<string> CallsFor(string deckId)
    {
        lock (_gate)
        {
            return [.. _calls.Where(c => c.Deck == deckId).Select(c => c.Call)];
        }
    }

    // Forgets every call.
    public void Reset()
    {
        lock (_gate)
        {
            _calls.Clear();
        }
    }

    public Task OpenAsync(string deckId, Guid sessionId) => RecordAsync(deckId, $"Open {sessionId}");

    public Task CloseAsync(string deckId, Guid sessionId) => RecordAsync(deckId, $"Close {sessionId}");

    public Task EnsureAsync(string deckId) => RecordAsync(deckId, "Ensure");

    private Task RecordAsync(string deckId, string call)
    {
        lock (_gate)
        {
            _calls.Add((deckId, call));
        }
        return Task.CompletedTask;
    }
}
