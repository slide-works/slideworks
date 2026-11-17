using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// GET /api/join/{code}/questions (guest-questions.json "view", Q4): what the room may do now, every open and answered
// question that isn't hidden plus this guest's own in any state, in number order (the phone sorts), and this guest's own
// upvotes. Also the "current" of a stale vote's 409.
public sealed record GuestQuestionsView(QaRoom Room, IReadOnlyList<GuestQuestion> Questions, MyVotes Mine);
