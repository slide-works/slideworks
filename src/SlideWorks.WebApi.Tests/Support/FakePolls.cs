using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Tests.Support;

// IPolls for tests that need a poll without the real lifecycle: each test sets the view a deck's slide answers with, and
// every action returns that view, or throws ConflictException with the code set by RefuseWith. It records each action as
// "Open 3" (the method without Async, then the slide), per deck, in order. Add it with FakePolls.AddTo. A slide with no
// view set answers null from ControlAsync, as the real one does for a slide that is not a poll.
public sealed class FakePolls : IPolls
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Deck, int Slide), PollControlView> _views = [];
    private readonly List<(string Deck, string Call)> _calls = [];
    private string? _refusal;

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakePolls>().AddSingleton<IPolls>(provider => provider.GetRequiredService<FakePolls>());

    public void Set(string deckId, int slide, PollControlView view)
    {
        lock (_gate)
        {
            _views[(deckId, slide)] = view;
        }
    }

    // Every action from now on throws ConflictException(code) and records nothing; null accepts them again.
    public void RefuseWith(string? code)
    {
        lock (_gate)
        {
            _refusal = code;
        }
    }

    public IReadOnlyList<string> CallsFor(string deckId)
    {
        lock (_gate)
        {
            return [.. _calls.Where(c => c.Deck == deckId).Select(c => c.Call)];
        }
    }

    public Task<PollControlView> OpenAsync(string deckId, int slide) => ActAsync("Open", deckId, slide);

    public Task<PollControlView> CloseAsync(string deckId, int slide) => ActAsync("Close", deckId, slide);

    public Task<PollControlView> RevealAsync(string deckId, int slide) => ActAsync("Reveal", deckId, slide);

    public Task<PollControlView> ShowAnswerAsync(string deckId, int slide) => ActAsync("ShowAnswer", deckId, slide);

    public Task<PollControlView> NewRoundAsync(string deckId, int slide) => ActAsync("NewRound", deckId, slide);

    public Task<PollControlView?> ControlAsync(string deckId, int slide)
    {
        lock (_gate)
        {
            return Task.FromResult(_views.GetValueOrDefault((deckId, slide)));
        }
    }

    private Task<PollControlView> ActAsync(string action, string deckId, int slide)
    {
        lock (_gate)
        {
            if (_refusal != null)
            {
                throw new ConflictException(_refusal);
            }
            if (!_views.TryGetValue((deckId, slide), out var view))
            {
                throw new InvalidOperationException($"FakePolls has no view for slide {slide} of deck {deckId}; call Set first.");
            }
            _calls.Add((deckId, $"{action} {slide}"));
            return Task.FromResult(view);
        }
    }
}
