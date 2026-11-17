namespace SlideWorks.WebApi.Features.Qa.Contracts;

// The body of a 201 to POST /api/join/{code}/questions (question-ask.json "created", Q4): the new question as the asker sees
// it, and the asker's upvotes, which include it unless the writer refused that upvote.
public sealed record AskedQuestion(GuestQuestion Question, MyVotes Mine);
