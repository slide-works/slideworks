namespace SlideWorks.WebApi.Features.Polls;

// The one publisher of pollControl and pollTally (V5). It reads the poll on the slide and its tally, stamps seq, and
// publishes the presenter's view to the watch group, the tally to the audience with counts only when the room sees them,
// and the tally with counts to the results group while an "afterAnswer" round is open. Called on every phase change, by the
// tally pump and after an option changes. A singleton.
public interface IPollPublisher
{
    Task PublishAsync(string deckId, int slide);
}
