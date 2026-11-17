namespace SlideWorks.WebApi.Features.Qa.Contracts;

// A guest's own upvotes in the Q&A round (Q4): the question numbers it upvoted, ascending, and the revision of that answer,
// 0 before its first vote. Both are integers. GuestQuestionsView.Mine, part of the ask route's 201 and the body of the
// votes route's 200.
public sealed record MyVotes(IReadOnlyList<int> Picks, int Revision);
