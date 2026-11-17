using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Qa;

namespace SlideWorks.WebApi.Tests.Support;

// IQuestionPublisher for tests of what calls the publisher: it publishes nothing and records each call's deck, in order, or
// throws what FailWith set (after recording), for a test that a failing publish doesn't fail its caller. Add it with
// FakeQuestionPublisher.AddTo. The app, and so this fake, is shared by every test in a fixture: each test uses deck ids of
// its own and reads PublishesOf its own deck, and a test that calls FailWith calls Reset when it is done.
public sealed class FakeQuestionPublisher : IQuestionPublisher
{
    private readonly Lock _gate = new();
    private readonly List<string> _published = [];
    private Exception? _failure;

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeQuestionPublisher>().AddSingleton<IQuestionPublisher>(provider => provider.GetRequiredService<FakeQuestionPublisher>());

    // How many times the deck was published.
    public int PublishesOf(string deckId)
    {
        lock (_gate)
        {
            return _published.Count(d => d == deckId);
        }
    }

    // Every publish from now on throws this; null publishes again.
    public void FailWith(Exception? failure)
    {
        lock (_gate)
        {
            _failure = failure;
        }
    }

    // Forgets every call and the failure.
    public void Reset()
    {
        lock (_gate)
        {
            _published.Clear();
            _failure = null;
        }
    }

    public Task PublishAsync(string deckId)
    {
        lock (_gate)
        {
            _published.Add(deckId);
            return _failure is null ? Task.CompletedTask : Task.FromException(_failure);
        }
    }
}
