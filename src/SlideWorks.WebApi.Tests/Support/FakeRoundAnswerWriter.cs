using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Tests.Support;

// IRoundAnswerWriter for tests of what calls the writer, without SW-6's batching writer or the tally: it writes nothing and
// records every call in order. By default a call is Written with the picks, ascending, and expectedRevision + 1; a test
// sets what a round's calls return with Answer (a function of the call, which may also throw, as a failed commit does).
// Add it with FakeRoundAnswerWriter.AddTo. The app, and so this fake, is shared by every test in a fixture: each test uses
// rounds of its own and reads WritesFor its own round.
public sealed class FakeRoundAnswerWriter : IRoundAnswerWriter
{
    private readonly Lock _gate = new();
    private readonly List<RoundAnswerCall> _calls = [];
    private readonly Dictionary<string, Func<RoundAnswerCall, RoundWrite>> _answers = new(StringComparer.Ordinal);

    public static void AddTo(IServiceCollection services)
        => services.AddSingleton<FakeRoundAnswerWriter>().AddSingleton<IRoundAnswerWriter>(provider => provider.GetRequiredService<FakeRoundAnswerWriter>());

    // The calls for this round id, in order.
    public IReadOnlyList<RoundAnswerCall> WritesFor(string roundId)
    {
        lock (_gate)
        {
            return [.. _calls.Where(c => c.Round.RoundId == roundId)];
        }
    }

    // What every call for this round id returns from now on, worked out from the call.
    public void Answer(string roundId, Func<RoundAnswerCall, RoundWrite> result)
    {
        lock (_gate)
        {
            _answers[roundId] = result;
        }
    }

    public Task<RoundWrite> ReplaceAsync(PollRoundRef round, string guestId, IReadOnlyList<int> picks, int expectedRevision)
    {
        var call = new RoundAnswerCall(round, guestId, [.. picks], expectedRevision);
        Func<RoundAnswerCall, RoundWrite>? answer;
        lock (_gate)
        {
            _calls.Add(call);
            answer = _answers.GetValueOrDefault(round.RoundId);
        }
        return Task.FromResult(answer is null ? new RoundWrite.Written([.. picks.Order()], expectedRevision + 1) : answer(call));
    }

    // One call to ReplaceAsync, as it was made.
    public sealed record RoundAnswerCall(PollRoundRef Round, string GuestId, IReadOnlyList<int> Picks, int ExpectedRevision);
}
