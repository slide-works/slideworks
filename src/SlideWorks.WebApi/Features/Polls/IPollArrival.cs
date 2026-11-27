namespace SlideWorks.WebApi.Features.Polls;

// What the presenter reaching a slide does to polls (V4, D2, D12), called by the arrival observer on every presentation
// state while a session is live. It closes an open round on any other slide and opens round 1 on this slide when it is an
// "onArrival" poll with no round yet in this session, by calling IPolls. When the presenter has moved onto the slide (moved),
// an "onArrival" poll whose round is closed is reopened, so a poll is open whenever the presenter is on it; a state that
// only repeats the slide (a step, the black screen, the timer) leaves a poll the presenter closed there closed. A state for
// the slide the open round is on does nothing. Per request, because it uses the database.
public interface IPollArrival
{
    Task ArrivedAsync(string deckId, int slide, bool moved);
}
