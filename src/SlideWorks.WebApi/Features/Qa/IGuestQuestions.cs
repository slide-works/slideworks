using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Features.Qa;

// A guest's view of the Q&A round and its upvotes (Q4). Code is the audience session's join code and guestId the sw_guest
// cookie's value. Nothing a guest receives says which guest asked or upvoted, and another guest's hidden or waiting question
// is never in it. Per request, because it uses the database.
public interface IGuestQuestions
{
    // Replaces the guest's upvotes with request.Picks, keeping its picks on questions that can't be voted on now, through
    // IRoundAnswerWriter. Nothing is written for any result but Voted.
    Task<VoteResult> VoteAsync(string code, string guestId, VoteRequest request);

    // The room, every open and answered question that isn't hidden plus this guest's own in any state, in number order, and
    // the guest's own upvotes. Null when there is no live session for the code or the guest isn't on its roster.
    Task<GuestQuestionsView?> ViewAsync(string code, string guestId);
}
