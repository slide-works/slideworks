using SlideWorks.Core.Qa;

namespace SlideWorks.WebApi.Features.Qa.Contracts;

// One question on a guest's phone (guest-questions.json, Q4). Number is the question's 1-based position in its round, the
// only id on the wire. State is open or answered, or for the guest's own question also waiting or hidden: another guest's
// hidden or waiting question is never sent. Name is null for an anonymous question. Age is a label the server makes
// (QuestionAge). Votes is an integer, frozen once answered. Yours is true on this guest's own question and is the only thing
// that ties a question to a guest: no view says which guest asked or upvoted.
public sealed record GuestQuestion(int Number, string Text, string? Name, string Age, QuestionStatus State, int Votes, bool OnScreen, bool Yours);
