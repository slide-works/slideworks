namespace SlideWorks.WebApi.Features.Qa;

// The one publisher of questionControl and questionTally (Q5). It reads IQuestionModeration.ControlAsync, stamps seq, and
// publishes the presenter's view to the watch group and, while a round is live, the votes of the questions another guest may
// see (not hidden, not waiting) to the audience. Moderation and settings changes call it after IAudienceRelay.RefreshAsync;
// the vote pump calls it at most twice a second per deck. A singleton.
public interface IQuestionPublisher
{
    Task PublishAsync(string deckId);
}
