using System.Text.Json.Serialization;
using SlideWorks.Core.Polls;

namespace SlideWorks.WebApi.Features.Polls.Contracts;

// What a guest's phone gets for a poll slide (poll-activity.json, V5): the value of audienceState.activity and of
// GuestPollView.Activity. Round is the open or closed round's id and null in draft. Number is the round's number, or in
// draft the number the next round will get. Correct is null until the presenter shows the answer, then the 1-based option
// number. It never carries counts, Opens or Privacy: counts travel only in a PollTally a guest may see (PollVisibility).
public sealed record PollActivity(
    string? Round,
    int Number,
    PollPhase Phase,
    string Question,
    IReadOnlyList<string> Options,
    PollPick Pick,
    int? Max,
    PollResults Results,
    bool RoomSeesCounts,
    int? Correct,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] PollAnswersForm Answers = PollAnswersForm.Choice)
{
    public const string TYPE = "poll";

    // The activity's kind, so a later activity (a scale, a word cloud) can share audienceState.activity.
    [JsonPropertyOrder(-1)]
    public string Type => TYPE;
}
