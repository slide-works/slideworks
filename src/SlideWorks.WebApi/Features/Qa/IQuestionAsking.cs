using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// A guest asking a question (Q4). Code is the audience session's join code and guestId the sw_guest cookie's value; the
// controller's helper has already resolved the code, so a guest not on the session's roster is NoSession, not a miss. Every
// check comes before any write: a refused ask writes nothing and doesn't use up the limit. An accepted ask stores the
// question (waiting when review is on), adds the asker's upvote through IRoundAnswerWriter (the question stands without it
// if the writer refuses), bumps QaRevisions and calls IQuestionPublisher. Per request, because it uses the database.
public interface IQuestionAsking
{
    Task<AskResult> AskAsync(string code, string guestId, AskRequest request);
}
