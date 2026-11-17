namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The body of PUT /api/join/{code}/questions/votes (question-votes.json "request", Q4): the Q&A round, every question number
// the guest upvotes now (the whole set, replaced) and the revision of the votes it holds (0 before its first vote). The
// picks and the revision are integers.
public sealed record VoteRequest(string Round, IReadOnlyList<int> Picks, int Revision);
