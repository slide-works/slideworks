using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IGuestQuestions for tests that need a guest's view or votes without the real rules: each test sets the view and the vote
// result a join code answers with, and every call records its code and guest (and the vote's request), in order. Add it with
// FakeGuestQuestions.AddTo. A code with no view set answers null, as the real one does for no session; a vote on a code with
// no result set throws. The app, and so this fake, is shared by every test in a fixture: each test uses a code of its own,
// or calls Reset when it is done.
public sealed class FakeGuestQuestions : IGuestQuestions
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, GuestQuestionsView> _views = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VoteResult> _votes = new(StringComparer.Ordinal);
    private readonly List<(string Code, string GuestId, VoteRequest Request)> _voteCalls = [];
    private readonly List<(string Code, string GuestId)> _viewCalls = [];

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeGuestQuestions>().AddSingleton<IGuestQuestions>(provider => provider.GetRequiredService<FakeGuestQuestions>());

    public void SetView(string code, GuestQuestionsView view)
    {
        lock (_gate)
        {
            _views[code] = view;
        }
    }

    public void SetVote(string code, VoteResult result)
    {
        lock (_gate)
        {
            _votes[code] = result;
        }
    }

    // Every vote for this code, in order.
    public IReadOnlyList<(string GuestId, VoteRequest Request)> VotesFor(string code)
    {
        lock (_gate)
        {
            return [.. _voteCalls.Where(c => c.Code == code).Select(c => (c.GuestId, c.Request))];
        }
    }

    // The guest of every view read for this code, in order.
    public IReadOnlyList<string> ViewsFor(string code)
    {
        lock (_gate)
        {
            return [.. _viewCalls.Where(c => c.Code == code).Select(c => c.GuestId)];
        }
    }

    // Forgets every view, result and call.
    public void Reset()
    {
        lock (_gate)
        {
            _views.Clear();
            _votes.Clear();
            _voteCalls.Clear();
            _viewCalls.Clear();
        }
    }

    public Task<VoteResult> VoteAsync(string code, string guestId, VoteRequest request)
    {
        lock (_gate)
        {
            _voteCalls.Add((code, guestId, request));
            return _votes.TryGetValue(code, out var result)
                ? Task.FromResult(result)
                : throw new InvalidOperationException($"FakeGuestQuestions has no vote result for code {code}; call SetVote first.");
        }
    }

    public Task<GuestQuestionsView?> ViewAsync(string code, string guestId)
    {
        lock (_gate)
        {
            _viewCalls.Add((code, guestId));
            return Task.FromResult(_views.GetValueOrDefault(code));
        }
    }
}
