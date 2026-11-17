namespace SlideWorks.Core.Qa;

// What the room may do on the slide on screen (qa-room.json, Q1): audienceState.room while live, and GuestQuestionsView.Room.
// Only QaRoomRules.For makes one; the phone reads it as it is. Round is the Q&A round id, null when Questions is off.
// ListRevision (Q5) is an integer that only increases within a round: a phone re-reads its list when it moves, and when
// Round changes. It is 0 when Round is null.
public sealed record QaRoom(string? Round, QaRoomQuestions Questions, bool Names, bool Reactions, long ListRevision);
