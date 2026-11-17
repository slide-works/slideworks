using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// The presenter's actions on a question and the view they return (Q6, D13): the only place, with IQuestionAsking.AskAsync,
// that changes a question's state, hidden or on-screen flag. Number is the question's 1-based position in the deck's open
// Q&A round. A refused action throws ConflictException with a code (not_live, wrong_state) and changes nothing; an unknown
// number throws NotFoundException. Each accepted action bumps QaRevisions, records activity (the action, never the text),
// then calls IAudienceRelay.RefreshAsync, then IQuestionPublisher.PublishAsync. Per request, because it uses the database.
public interface IQuestionModeration
{
    Task<QaControlView> ApproveAsync(string deckId, int number);

    Task<QaControlView> HideAsync(string deckId, int number);

    Task<QaControlView> RestoreAsync(string deckId, int number);

    Task<QaControlView> AnswerAsync(string deckId, int number);

    Task<QaControlView> ReopenAsync(string deckId, int number);

    Task<QaControlView> ShowAsync(string deckId, int number);

    Task<QaControlView> UnshowAsync(string deckId, int number);

    // The presenter's view of the deck's Q&A: every question of the open round with its votes, the counts, the settings, the
    // switch of the presenter's slide and the switched-off slides, and the session's reaction totals. Not live: Live false,
    // no round, no questions and zero counts, with the settings. Seq comes from QaSeq.
    Task<QaControlView> ControlAsync(string deckId);
}
