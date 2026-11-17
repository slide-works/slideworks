using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;

namespace SlideWorks.WebApi.Tests.Support;

// IPollPublisher for tests of what calls the publisher: it publishes nothing and records each call as (deck, slide), in
// order. Add it with FakePollPublisher.AddTo. The app, and so this fake, is shared by every test in a fixture: each test
// uses deck ids of its own and reads PublishedFor its own deck.
public sealed class FakePollPublisher : IPollPublisher
{
    private readonly Lock _gate = new();
    private readonly List<(string DeckId, int Slide)> _published = [];

    // The slides published for this deck, in order.
    public IReadOnlyList<int> PublishedFor(string deckId)
    {
        lock (_gate)
        {
            return [.. _published.Where(p => p.DeckId == deckId).Select(p => p.Slide)];
        }
    }

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakePollPublisher>().AddSingleton<IPollPublisher>(provider => provider.GetRequiredService<FakePollPublisher>());

    public Task PublishAsync(string deckId, int slide)
    {
        lock (_gate)
        {
            _published.Add((deckId, slide));
        }
        return Task.CompletedTask;
    }
}
