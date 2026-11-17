using Agoda.IoC.Core;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Polls;

// The one publisher of pollControl and pollTally (V5). For the poll on a slide it publishes, in this order:
// - pollControl, the presenter's view with the tally's answered and counts, for audience-watch:{deckId};
// - pollTally for the audience, with counts only when PollVisibility says the room sees them;
// - pollTally for the results group, always with counts, while an "afterAnswer" round is open.
// A slide that is not a poll publishes nothing, and a draft (no round yet) publishes only the presenter's view. Each event
// carries its Target or its type; which hub group gets it is AudienceEventsRelay's job.
//
// Seq is taken before anything is read, as the audience feed does, so a publish that follows a change always carries a
// higher seq than any publish that read from before it, and the client's "drop a lower seq" keeps the newest. A singleton:
// IPolls is per request, so it is resolved in a scope of its own on each call.
[RegisterSingleton(For = typeof(IPollPublisher))]
public sealed class PollPublisher(IServiceScopeFactory scopes, IPollTallies tallies, IAudienceEvents events, PollSeq seq) : IPollPublisher
{
    public async Task PublishAsync(string deckId, int slide)
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
        if (control.Round is not { } round)
        {
            await events.PublishAsync(new PollControl(deckId, control with { Seq = stamp }));
            return;
        }
        var tally = await tallies.ReadAsync(round, control.Options.Count);
        var roomSees = PollVisibility.RoomSees(control.Settings.Results, control.Revealed, control.Phase == PollPhase.Closed);
        await events.PublishAsync(new PollControl(deckId, control with { Seq = stamp, Answered = tally.Answered, Counts = tally.Counts }));
        await events.PublishAsync(new PollTally(deckId, PollTallyTarget.Audience, round, tally.Answered, roomSees ? tally.Counts : null));
        if (control.Settings.Results == PollResults.AfterAnswer && control.Phase == PollPhase.Open)
        {
            await events.PublishAsync(new PollTally(deckId, PollTallyTarget.Results, round, tally.Answered, tally.Counts));
        }
    }
}
