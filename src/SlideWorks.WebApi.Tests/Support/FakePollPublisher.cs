using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;

namespace SlideWorks.WebApi.Tests.Support;

// IPollPublisher for tests of what calls the publisher: it publishes nothing and records each call as (deck, slide), in
// order. Add it with FakePollPublisher.AddTo.
public sealed class FakePollPublisher : IPollPublisher
{
    private readonly Lock _gate = new();
    private readonly List<(string DeckId, int Slide)> _published = [];

    public IReadOnlyList<(string DeckId, int Slide)> Published
    {
        get
        {
            lock (_gate)
            {
                return [.. _published];
            }
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
