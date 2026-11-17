using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa;

namespace SlideWorks.WebApi.Tests.Support;

// IAudienceRoom for tests that need a room without the real settings and round: each test sets the room a deck answers
// with, and every read records the deck, in order. A deck with no room set answers null, as the real one does when the deck
// isn't live. Add it with FakeAudienceRoom.AddTo. The app, and so this fake, is shared by every test in a fixture: each test
// uses deck ids of its own, or calls Reset when it is done.
public sealed class FakeAudienceRoom : IAudienceRoom
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, QaRoom?> _rooms = new(StringComparer.Ordinal);
    private readonly List<string> _reads = [];

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeAudienceRoom>().AddSingleton<IAudienceRoom>(provider => provider.GetRequiredService<FakeAudienceRoom>());

    // The room the deck answers with from now on; null for not live.
    public void Set(string deckId, QaRoom? room)
    {
        lock (_gate)
        {
            _rooms[deckId] = room;
        }
    }

    // How many times the deck's room was read.
    public int ReadsOf(string deckId)
    {
        lock (_gate)
        {
            return _reads.Count(d => d == deckId);
        }
    }

    // Forgets every room and read.
    public void Reset()
    {
        lock (_gate)
        {
            _rooms.Clear();
            _reads.Clear();
        }
    }

    public Task<QaRoom?> ForDeckAsync(string deckId)
    {
        lock (_gate)
        {
            _reads.Add(deckId);
            return Task.FromResult(_rooms.GetValueOrDefault(deckId));
        }
    }
}
