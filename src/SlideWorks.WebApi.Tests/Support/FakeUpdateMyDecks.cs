using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.Support;

// IUpdateMyDecks for tests of the "Update my decks" route and its callers: it answers the count a test sets with Answer
// (0 until then), or throws what a test hands to Refuse. Calls are recorded in order as "Run <template id> <email>"; a
// refused call records nothing. Add it with FakeUpdateMyDecks.Register. The app, and so this fake, is shared by every test
// in a fixture: a test that sets an answer or a refusal calls Reset when it is done.
public sealed class FakeUpdateMyDecks : IUpdateMyDecks
{
    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];
    private int _count;
    private Exception? _refusal;

    public static void Register(IServiceCollection services)
        => services.AddSingleton<FakeUpdateMyDecks>().AddSingleton<IUpdateMyDecks>(provider => provider.GetRequiredService<FakeUpdateMyDecks>());

    public void Answer(int count)
    {
        lock (_gate)
        {
            _count = count;
        }
    }

    // Every call from now on throws this and records nothing; null accepts them again.
    public void Refuse(Exception? refusal)
    {
        lock (_gate)
        {
            _refusal = refusal;
        }
    }

    // Back to answering 0, with no refusal and no recorded calls.
    public void Reset()
    {
        lock (_gate)
        {
            _count = 0;
            _refusal = null;
            _calls.Clear();
        }
    }

    public IReadOnlyList<string> Calls()
    {
        lock (_gate)
        {
            return [.. _calls];
        }
    }

    public Task<int> RunAsync(ICurrentActor actor, string templateId, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_refusal != null)
            {
                throw _refusal;
            }
            _calls.Add($"Run {templateId} {actor.Email}");
            return Task.FromResult(_count);
        }
    }
}
