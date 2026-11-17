namespace SlideWorks.WebApi.Features.Polls.Contracts;

// GET /api/join/{code}/poll (guest-poll.json, V5): the poll on screen, this guest's own answer and the tally. Activity is
// null on a slide with no poll. Mine is null until this guest has answered in the round. Tally is null with no round;
// its Counts are null while PollVisibility.GuestSees is false for this guest, and Answered is always there (D21).
// JoinedLate is true only when the round is closed, this guest has no answer in it and joined the session after it
// closed ("You joined after this poll closed"); false otherwise.
public sealed record GuestPollView(PollActivity? Activity, MyAnswer? Mine, PollTally? Tally, bool JoinedLate);
