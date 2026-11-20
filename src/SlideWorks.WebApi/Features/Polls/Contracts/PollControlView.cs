using System.Text.Json.Serialization;
using SlideWorks.Core.Polls;

namespace SlideWorks.WebApi.Features.Polls.Contracts;

// What the presenter's controls show for a poll slide (poll-control.json, V4): every poll route's response and the
// pollControl event's payload. Seq only increases (PollSeq), so a late view is dropped. Slide is the 1-based slide number.
// Round is the round id, null in draft; Number is the round's number, or in draft the number it gets when opened. Counts
// are always there, because a person who can present always sees them; RoomSeesCounts says whether the room does too.
// Rounds lists this session's closed rounds, oldest first.
public sealed record PollControlView(
    long Seq,
    int Slide,
    string? Round,
    int Number,
    PollPhase Phase,
    PollSettings Settings,
    string Question,
    IReadOnlyList<string> Options,
    int Answered,
    int Joined,
    IReadOnlyList<int> Counts,
    bool RoomSeesCounts,
    bool Revealed,
    bool AnswerShown,
    IReadOnlyList<PollRoundSummary> Rounds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Replies = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PollWord>? Words = null);
