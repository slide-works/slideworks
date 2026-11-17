namespace SlideWorks.Core.Polls;

// A poll slide's settings (V1, D10): one Poll row per poll slide, never slots, because slots go to every viewer and
// Correct must not. Max is set only when Pick is UpTo, from 2 to the option count. Correct is a 1-based option number or
// null. PollSettingsRules is the one place they are checked against the slide's options.
public sealed record PollSettings(PollPick Pick, int? Max, PollResults Results, int? Correct, PollOpens Opens, PollPrivacy Privacy)
{
    // What a new poll slide starts with: pick one, results live, no correct answer, opens on arrival, private.
    public static PollSettings Default { get; } = new(PollPick.One, null, PollResults.Live, null, PollOpens.OnArrival, PollPrivacy.Private);
}
