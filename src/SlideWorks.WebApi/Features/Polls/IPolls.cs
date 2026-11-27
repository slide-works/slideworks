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

    // Opens the poll's last closed round again, with the answers it already has, so the room can go on answering it. Its
    // Final, JoinedAtClose and shown answer are cleared and written again when it next closes. Refused (wrong_phase) unless
    // the poll is closed in the live session.
    Task<PollControlView> ReopenAsync(string deckId, int slide);

    // The presenter's view of the poll on that slide in the deck's current audience session, or null when the slide is not
    // a poll. Seq comes from PollSeq. A closed round's Answered and Counts are its Final snapshot, written in the close
    // transaction; an open round's come from IPollTallies.
    Task<PollControlView?> ControlAsync(string deckId, int slide);

    // Ending the live session: closes the session's open round with its Final, as CloseAsync does, or discards its draft
    // (ClosedAt with no Final). Called once from LiveCommands.EndAsync, with the session it is about to end, before it is
    // ended. Does nothing when the session has no unclosed round.
    Task EndSessionAsync(string deckId, Guid sessionId);
}
