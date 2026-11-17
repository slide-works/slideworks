using SlideWorks.Core.Polls;

namespace SlideWorks.WebApi.Features.Polls.Data;

// A poll slide's settings (V1, D10): one row per poll slide, keyed by the slide, made with the defaults when the slide is
// added as a poll or its layout becomes poll (PollRows.EnsureAsync). The question and the options are the slide's slots;
// nothing here goes to every viewer, because Correct must not. The row is kept when the slide is deleted or its layout
// changes, and never removed.
public class Poll
{
    public Guid SlideId { get; set; }

    public required string DeckId { get; set; }

    public PollPick Pick { get; set; }

    // Set only when Pick is UpTo, 2 to the option count (PollSettingsRules).
    public int? Max { get; set; }

    public PollResults Results { get; set; }

    // A 1-based option number, or null.
    public int? Correct { get; set; }

    public PollOpens Opens { get; set; }

    public PollPrivacy Privacy { get; set; }

    public static Poll From(Guid slideId, string deckId, PollSettings settings) => new()
    {
        SlideId = slideId,
        DeckId = deckId,
        Pick = settings.Pick,
        Max = settings.Max,
        Results = settings.Results,
        Correct = settings.Correct,
        Opens = settings.Opens,
        Privacy = settings.Privacy,
    };

    public PollSettings ToSettings() => new(Pick, Max, Results, Correct, Opens, Privacy);
}
