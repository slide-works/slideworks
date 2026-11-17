using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IPollAnswers for tests that need answers without the writer: each test sets what a submit returns (by default it accepts
// the picks with the next revision), the view a code answers with, a guest's own answer and the round a guest may watch.
// It records every submit in order. Add it with FakePollAnswers.AddTo. Anything not set answers null, as the real one does
// for an unknown code, a guest with no answer or a guest who may not watch.
public sealed class FakePollAnswers : IPollAnswers
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, GuestPollView> _views = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Round, string Guest), MyAnswer> _mine = [];
    private readonly Dictionary<(string Code, string Guest), string> _watch = [];
    private readonly List<(string Code, string GuestId, AnswerRequest Request)> _submitted = [];
    private Func<AnswerRequest, AnswerResult> _result = request => new AnswerResult.Accepted(request.Picks, request.Revision + 1);

    public IReadOnlyList<(string Code, string GuestId, AnswerRequest Request)> Submitted
    {
        get
        {
            lock (_gate)
            {
                return [.. _submitted];
            }
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
