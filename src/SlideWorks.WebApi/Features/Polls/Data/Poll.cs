using SlideWorks.Core.Polls;

namespace SlideWorks.WebApi.Features.Polls.Data;

// A poll's settings (V1, D10; SW-9 Q2). A slide poll is one row per poll slide, made with the defaults when the slide is
// added as a poll or its layout becomes poll (PollRows.EnsureAsync); its question and options are the slide's slots. A
// deck's Q&A poll (Kind Qa, at most one per deck, no slide) has a PollOption row per question in each round. Nothing here
// goes to every viewer, because Correct must not. The row is kept when the slide is deleted or its layout changes, and
// never removed.
public class Poll
{
    public Guid Id { get; set; }

    public PollKind Kind { get; set; }

    // The poll slide, for Kind Slide; null for the deck's Q&A poll.
    public Guid? SlideId { get; set; }

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
        Id = Guid.NewGuid(),
        Kind = PollKind.Slide,
        SlideId = slideId,
        DeckId = deckId,
        Pick = settings.Pick,
        Max = settings.Max,
        Results = settings.Results,
        Correct = settings.Correct,
        Opens = settings.Opens,
        Privacy = settings.Privacy,
    };

    // The deck's Q&A poll (SW-9 Q2): pick any (a guest's upvotes), results live, opened by the Q&A rounds service and never
    // on arrival, private, no slide. The one place its settings are written.
    public static Poll ForQa(string deckId) => new()
    {
        Id = Guid.NewGuid(),
        Kind = PollKind.Qa,
        SlideId = null,
        DeckId = deckId,
        Pick = PollPick.Any,
        Max = null,
        Results = PollResults.Live,
        Correct = null,
        Opens = PollOpens.Manual,
        Privacy = PollPrivacy.Private,
    };

    public PollSettings ToSettings() => new(Pick, Max, Results, Correct, Opens, Privacy);
}
