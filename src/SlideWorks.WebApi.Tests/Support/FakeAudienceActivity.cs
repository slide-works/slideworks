using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IAudienceActivity for tests that need an activity on screen without a poll: each test sets the activity a deck and its
// join code answer with. Add it with FakeAudienceActivity.AddTo. A deck or code with none set answers null, as the real
// one does on a slide with no poll.
public sealed class FakeAudienceActivity : IAudienceActivity
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, PollActivity?> _byDeck = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PollActivity?> _byCode = new(StringComparer.Ordinal);

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeAudienceActivity>().AddSingleton<IAudienceActivity>(provider => provider.GetRequiredService<FakeAudienceActivity>());

    // The deck and its code now answer with this activity; null clears it.
    public void Set(string code, string deckId, PollActivity? activity)
    {
        lock (_gate)
        {
            _byDeck[deckId] = activity;
            _byCode[code] = activity;
        }
    }

    public Task<PollActivity?> ForDeckAsync(string deckId)
    {
        lock (_gate)
        {
            return Task.FromResult(_byDeck.GetValueOrDefault(deckId));
        }
    }

    public Task<PollActivity?> ForCodeAsync(string code)
    {
        lock (_gate)
        {
            return Task.FromResult(_byCode.GetValueOrDefault(code));
        }
    }
}
