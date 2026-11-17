using SlideWorks.WebApi.Features.Polls.Contracts;

namespace SlideWorks.WebApi.Features.Polls;

// The one place a poll round is opened, closed or changed (V4). Slide is the 1-based slide number, resolved to the slide at
// call time. Each action returns the presenter's view after it; a refused action throws ConflictException with a
// PollRefusals code and writes nothing. Every phase change records activity, then calls IAudienceRelay.RefreshAsync, then
// IPollPublisher.PublishAsync. It knows nothing of arrival: IPollArrival calls it, never the other way round. Per request,
// because it uses the database.
public interface IPolls
{
    Task<PollControlView> OpenAsync(string deckId, int slide);

    Task<PollControlView> CloseAsync(string deckId, int slide);

    Task<PollControlView> RevealAsync(string deckId, int slide);

    Task<PollControlView> ShowAnswerAsync(string deckId, int slide);

    Task<PollControlView> NewRoundAsync(string deckId, int slide);

    // The presenter's view of the poll on that slide in the deck's current audience session, or null when the slide is not
    // a poll. Seq comes from PollSeq.
    Task<PollControlView?> ControlAsync(string deckId, int slide);
}
