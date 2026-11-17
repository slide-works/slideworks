using System.Collections.Concurrent;
using Agoda.IoC.Core;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls;

// The one publisher of pollControl and pollTally (V5). For the poll on a slide it publishes, in this order:
// - pollControl, the presenter's view, for audience-watch:{deckId};
// - pollTally for the audience, with counts only when PollVisibility says the room sees them;
// - pollTally for the results group, always with counts, while an "afterAnswer" round is open.
// A slide that is not a poll publishes nothing, and a draft publishes only the presenter's view. While the round is open,
// Answered and Counts come from IPollTallies; once it is closed they are the view's own, its Final snapshot, and the tally is
// not read. Each event carries its Target or its type; which hub group gets it is AudienceEventsRelay's job.
//
// Publishes for one deck run one at a time behind the deck's gate, with seq taken inside it, so one publish's events all go
// out before the next one's, and the last tally on the wire is from the last read. pollTally carries no seq, so without
// the gate a slow publish that read an open tally could land after the close and leave phones on a stale count. A
// singleton: IPolls is per request, so it is resolved in a scope of its own on each call.
[RegisterSingleton(For = typeof(IPollPublisher))]
public sealed class PollPublisher(IServiceScopeFactory scopes, IPollTallies tallies, IAudienceEvents events, PollSeq seq) : IPollPublisher
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public async Task PublishAsync(string deckId, int slide)
    {
        var gate = _gates.GetOrAdd(deckId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            await PublishInsideGateAsync(deckId, slide);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task PublishInsideGateAsync(string deckId, int slide)
    {
        var stamp = seq.Next();
        PollControlView? control;
        await using (var scope = scopes.CreateAsyncScope())
        {
            control = await scope.ServiceProvider.GetRequiredService<IPolls>().ControlAsync(deckId, slide);
        }
        if (control is null)
        {
            return;
        }
        if (control.Phase == PollPhase.Draft || control.Round is not { } round)
        {
            await events.PublishAsync(new PollControl(deckId, control with { Seq = stamp }));
            return;
        }
        var open = control.Phase == PollPhase.Open;
        var tally = open ? await tallies.ReadAsync(round, control.Options.Count) : new PollCounts(control.Answered, control.Counts);
        var roomSees = PollVisibility.RoomSees(control.Settings.Results, control.Revealed, !open);
        await events.PublishAsync(new PollControl(deckId, control with { Seq = stamp, Answered = tally.Answered, Counts = tally.Counts }));
        await events.PublishAsync(new PollTally(deckId, PollTallyTarget.Audience, round, tally.Answered, roomSees ? tally.Counts : null));
        if (open && control.Settings.Results == PollResults.AfterAnswer)
        {
            await events.PublishAsync(new PollTally(deckId, PollTallyTarget.Results, round, tally.Answered, tally.Counts));
        }
    }
}
