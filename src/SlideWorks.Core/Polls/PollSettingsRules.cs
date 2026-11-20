namespace SlideWorks.Core.Polls;

// The one check of a poll's settings against its option count (V1). The failure names the field first, like the slide
// validator's errors, such as "max: must be 2 to 4".
public static class PollSettingsRules
{
    // The first rule the settings break, or null when they are valid for a poll with this many options.
    public static string? Check(PollSettings settings, int optionCount)
    {
        if (settings.Answers != PollAnswersForm.Choice)
        {
            if (optionCount != 0)
            {
                return "options: an open question has none";
            }

            if (settings.Pick != PollPick.One || settings.Max is not null)
            {
                return "pick: an open question takes one free answer";
            }

            return settings.Correct is not null ? "correct: an open question has no correct option" : null;
        }

        // A choice poll's options are the slide's. Q&A upvotes (pick any) are not a slide's options, so they are not held to 2.
        if (settings.Pick != PollPick.Any && optionCount < 2)
        {
            return "options: a choice poll needs 2 to 8";
        }

        // One has a single pick and Any (SW-9's upvotes) has no maximum, so only UpTo takes one.
        if (settings.Pick != PollPick.UpTo && settings.Max is not null)
        {
            return "max: only when pick is upTo";
        }

        if (settings.Pick == PollPick.UpTo && settings.Max is null)
        {
            return "max: required when pick is upTo";
        }

        if (settings.Max is { } max && (max < 2 || max > optionCount))
        {
            return $"max: must be 2 to {optionCount}";
        }

        if (settings.Correct is { } correct && (correct < 1 || correct > optionCount))
        {
            return $"correct: must be 1 to {optionCount}";
        }

        return null;
    }
}
