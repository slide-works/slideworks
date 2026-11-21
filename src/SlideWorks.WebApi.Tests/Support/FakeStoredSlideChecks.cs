using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Checks;

namespace SlideWorks.WebApi.Tests.Support;

// The test double for IStoredSlideChecks. Swap it in on a fixture with FakeStoredSlideChecks.Register (from ConfigureTestServices).
// It answers the findings given for the slide numbers asked about, in any deck.
public sealed class FakeStoredSlideChecks : IStoredSlideChecks
{
    private readonly ConcurrentDictionary<int, IReadOnlyList<SlideFinding>> _findings = new();

    public static void Register(IServiceCollection services)
        => services.AddSingleton<FakeStoredSlideChecks>().AddSingleton<IStoredSlideChecks>(provider => provider.GetRequiredService<FakeStoredSlideChecks>());

    public void Reset() => _findings.Clear();

    public void FindingsFor(int slide, params SlideFinding[] findings) => _findings[slide] = findings;

    public Task<IReadOnlyList<SlideFinding>> CheckAsync(string deckId, IReadOnlyList<int> slides, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SlideFinding>>([.. slides.SelectMany(n => _findings.GetValueOrDefault(n, []))]);
}
