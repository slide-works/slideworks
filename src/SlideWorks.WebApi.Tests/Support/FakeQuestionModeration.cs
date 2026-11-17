using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Tests.Support;

// IQuestionModeration for tests that need the presenter's view without the real rules: each test sets the view a deck
// answers with, and every action returns it, or throws what FailWith (or RefuseWith, a ConflictException with that code) set.
// It records each action as "Hide 3" (the method without Async, then the number), per deck, in order; ControlAsync is not
// recorded. Add it with FakeQuestionModeration.AddTo. A deck with no view set throws, so a missing Set shows at once. The
// app, and so this fake, is shared by every test in a fixture: each test uses deck ids of its own, and a test that calls
// FailWith or RefuseWith (which apply to every deck) calls Reset when it is done.
public sealed class FakeQuestionModeration : IQuestionModeration
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, QaControlView> _views = new(StringComparer.Ordinal);
    private readonly List<(string Deck, string Call)> _calls = [];
    private Exception? _failure;

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeQuestionModeration>().AddSingleton<IQuestionModeration>(provider => provider.GetRequiredService<FakeQuestionModeration>());

    public void Set(string deckId, QaControlView view)
    {
        lock (_gate)
        {
            _views[deckId] = view;
        }
    }

    // Every action from now on throws this and records nothing; null accepts them again. ControlAsync still answers.
    public void FailWith(Exception? failure)
    {
        lock (_gate)
        {
            _failure = failure;
        }
    }

    // Every action from now on throws ConflictException(code), as a refused action does; null accepts them again.
    public void RefuseWith(string? code) => FailWith(code is null ? null : new ConflictException(code));

    public IReadOnlyList<string> CallsFor(string deckId)
    {
        lock (_gate)
        {
            return [.. _calls.Where(c => c.Deck == deckId).Select(c => c.Call)];
        }
    }

    // Forgets every view, call and failure.
    public void Reset()
    {
        lock (_gate)
        {
            _views.Clear();
            _calls.Clear();
            _failure = null;
        }
    }

    public Task<QaControlView> ApproveAsync(string deckId, int number) => ActAsync("Approve", deckId, number);

    public Task<QaControlView> HideAsync(string deckId, int number) => ActAsync("Hide", deckId, number);

    public Task<QaControlView> RestoreAsync(string deckId, int number) => ActAsync("Restore", deckId, number);

    public Task<QaControlView> AnswerAsync(string deckId, int number) => ActAsync("Answer", deckId, number);

    public Task<QaControlView> ReopenAsync(string deckId, int number) => ActAsync("Reopen", deckId, number);

    public Task<QaControlView> ShowAsync(string deckId, int number) => ActAsync("Show", deckId, number);

    public Task<QaControlView> UnshowAsync(string deckId, int number) => ActAsync("Unshow", deckId, number);

    public Task<QaControlView> ControlAsync(string deckId)
    {
        lock (_gate)
        {
            return Task.FromResult(ViewOf(deckId));
        }
    }

    private Task<QaControlView> ActAsync(string action, string deckId, int number)
    {
        lock (_gate)
        {
            if (_failure != null)
            {
                throw _failure;
            }
            var view = ViewOf(deckId);
            _calls.Add((deckId, $"{action} {number}"));
            return Task.FromResult(view);
        }
    }

    private QaControlView ViewOf(string deckId)
        => _views.TryGetValue(deckId, out var view)
            ? view
            : throw new InvalidOperationException($"FakeQuestionModeration has no view for deck {deckId}; call Set first.");
}
