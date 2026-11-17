using System.Collections.Concurrent;
using Agoda.IoC.Core;
using SlideWorks.Core.Qa;
using SlideWorks.WebApi.Features.Qa.Contracts;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Qa;

// The one publisher of questionControl and questionTally (Q5). For a deck it publishes, in this order:
// - questionControl, the presenter's whole view from IQuestionModeration.ControlAsync, for audience-watch:{deckId};
// - questionTally, while the deck is live with a Q&A round, for audience:{deckId}: the round's list revision from
//   QaRevisions and the votes of only the questions another guest may see, that is neither hidden nor waiting.
// Not live, or live with no round (questions off): the control only. Each event carries its type; which hub group gets it is
// AudienceEventsRelay's job.
//
// Publishes for one deck run one at a time behind the deck's gate, with seq taken inside it, so one publish's events all go
// out before the next one's and the last tally on the wire is from the last read. The gate is this publisher's own: it
// never runs inside the audience feed's. A singleton: IQuestionModeration is per request, so it is resolved in a scope of
// its own on each call.
[RegisterSingleton(For = typeof(IQuestionPublisher))]
public sealed class QuestionPublisher(IServiceScopeFactory scopes, IAudienceEvents events, QaSeq seq, QaRevisions revisions) : IQuestionPublisher
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public async Task PublishAsync(string deckId)
    {
        var gate = _gates.GetOrAdd(deckId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            await PublishInsideGateAsync(deckId);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task PublishInsideGateAsync(string deckId)
    {
        var stamp = seq.Next();
        QaControlView control;
        await using (var scope = scopes.CreateAsyncScope())
        {
            control = await scope.ServiceProvider.GetRequiredService<IQuestionModeration>().ControlAsync(deckId);
        }
        await events.PublishAsync(new QuestionControl(deckId, control with { Seq = stamp }));
        if (!control.Live || control.Round is not { } round)
        {
            return;
        }
        var votes = control.Questions
            .Where(q => !q.Hidden && q.State != QuestionState.Waiting)
            .OrderBy(q => q.Number)
            .Select(q => new QuestionVotes(q.Number, q.Votes))
            .ToList();
        await events.PublishAsync(new QuestionTally(deckId, round, revisions.Current(round), votes));
    }
}
