using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;

namespace SlideWorks.WebApi.Tests.Support;

// IPollArrival for tests of what calls it (the arrival observer): it changes no poll and records each slide reached, per
// deck, in order. Add it with FakePollArrival.AddTo. The app, and so this fake, is shared by every test in a fixture: each
// test uses deck ids of its own.
public sealed class FakePollArrival : IPollArrival
{
    private readonly Lock _gate = new();
    private readonly List<(string DeckId, int Slide)> _arrivals = [];

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakePollArrival>().AddSingleton<IPollArrival>(provider => provider.GetRequiredService<FakePollArrival>());

    // The slides reached on this deck, in the order ArrivedAsync was called.
    public IReadOnlyList<int> ArrivalsFor(string deckId)
    {
        lock (_gate)
        {
            return [.. _arrivals.Where(a => a.DeckId == deckId).Select(a => a.Slide)];
        }
    }

    public Task ArrivedAsync(string deckId, int slide)
    {
        lock (_gate)
        {
            _arrivals.Add((deckId, slide));
        }
        return Task.CompletedTask;
    }
}
