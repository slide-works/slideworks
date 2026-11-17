namespace SlideWorks.WebApi.Features.Polls.Contracts;

// A guest's own answer in the round (V5): the 0-based option positions, ascending, and its revision, which starts at 1 and
// goes up by one on each replace. It is GuestPollView.Mine and the body of a 200 to PUT /api/join/{code}/poll/answer.
public sealed record MyAnswer(IReadOnlyList<int> Picks, int Revision);
