using System.Text.Json.Serialization;

namespace SlideWorks.Core.Polls;

// A poll slide's settings (V1, D10): one Poll row per poll slide, never slots, because slots go to every viewer and
// Correct must not. Max is set only when Pick is UpTo, from 2 to the option count. Correct is a 1-based option number or
// null. Answers is choice (the option list), text or cloud. PollSettingsRules is the one place they are checked against
// the slide's options. Choice is left off the wire, so a settings object from before open questions still reads.
public sealed record PollSettings(
    PollPick Pick,
    int? Max,
    PollResults Results,
    int? Correct,
    PollOpens Opens,
    PollPrivacy Privacy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] PollAnswersForm Answers = PollAnswersForm.Choice)
{
    // What a new poll slide starts with: pick one, results live, no correct answer, opens on arrival, private, a choice.
    public static PollSettings Default { get; } = new(PollPick.One, null, PollResults.Live, null, PollOpens.OnArrival, PollPrivacy.Private);
}
