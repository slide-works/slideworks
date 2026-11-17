using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IPollAnswers for tests that need answers without the writer: each test sets what a submit returns (by default it accepts
// the picks with the next revision), the view a code answers with, a guest's own answer and the round a guest may watch.
// It records every submit in order. Add it with FakePollAnswers.AddTo. Anything not set answers null, as the real one does
// for an unknown code, a guest with no answer or a guest who may not watch. The app, and so this fake, is shared by every
// test in a fixture: each test uses codes, round ids and guests of its own, reads SubmittedFor its own code, and a test
// that calls Answer calls Reset when it is done (Answer is for every code).
public sealed class FakePollAnswers : IPollAnswers
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, GuestPollView> _views = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Round, string Guest), MyAnswer> _mine = [];
    private readonly Dictionary<(string Code, string Guest), string> _watch = [];
    private readonly List<(string Code, string GuestId, AnswerRequest Request)> _submitted = [];
    private static readonly Func<AnswerRequest, AnswerResult> _accept = request => new AnswerResult.Accepted(request.Picks, request.Revision + 1);
    private Func<AnswerRequest, AnswerResult> _result = _accept;

    // The submits for this code, in order.
    public IReadOnlyList<(string GuestId, AnswerRequest Request)> SubmittedFor(string code)
    {
        lock (_gate)
        {
            return [.. _submitted.Where(s => s.Code == code).Select(s => (s.GuestId, s.Request))];
        }
    }

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakePollAnswers>().AddSingleton<IPollAnswers>(provider => provider.GetRequiredService<FakePollAnswers>());

    // What every submit from now on returns, worked out from its request.
    public void Answer(Func<AnswerRequest, AnswerResult> result)
    {
        lock (_gate)
        {
            _result = result;
        }
    }

    // Submits accept again, with the picks and the next revision.
    public void Reset() => Answer(_accept);

    // The view every guest of this code gets.
    public void SetView(string code, GuestPollView view)
    {
        lock (_gate)
        {
            _views[code] = view;
        }
    }

    public void SetMine(string roundId, string guestId, MyAnswer mine)
    {
        lock (_gate)
        {
            _mine[(roundId, guestId)] = mine;
        }
    }

    public void SetWatch(string code, string guestId, string roundId)
    {
        lock (_gate)
        {
            _watch[(code, guestId)] = roundId;
        }
    }

    public Task<AnswerResult> SubmitAsync(string code, string guestId, AnswerRequest request)
    {
        lock (_gate)
        {
            _submitted.Add((code, guestId, request));
            return Task.FromResult(_result(request));
        }
    }

    public Task<GuestPollView?> ViewAsync(string code, string guestId)
    {
        lock (_gate)
        {
            return Task.FromResult(_views.GetValueOrDefault(code));
        }
    }

    public Task<MyAnswer?> MineAsync(string roundId, string guestId)
    {
        lock (_gate)
        {
            return Task.FromResult(_mine.GetValueOrDefault((roundId, guestId)));
        }
    }

    public Task<string?> CanWatchAsync(string code, string guestId)
    {
        lock (_gate)
        {
            return Task.FromResult(_watch.GetValueOrDefault((code, guestId)));
        }
    }
}
