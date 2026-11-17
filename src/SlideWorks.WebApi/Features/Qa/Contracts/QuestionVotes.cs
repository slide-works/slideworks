namespace SlideWorks.WebApi.Features.Qa.Contracts;

// One question's upvotes in a questionTally (Q5): its 1-based number and its vote count, both integers.
public sealed record QuestionVotes(int Number, int Votes);
