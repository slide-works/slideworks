namespace SlideWorks.WebApi.Realtime;

// The Reading in counts of a deck (SW-886 K11): guests connected now, by the language of the slide-state group their
// connection is placed in. The primary comes first and is always present, even at 0; the other languages follow in the
// deck's list order and are left out at 0.
public interface IReadingIn
{
    // Something that can change the counts happened on the deck. Marks it for the pump; never blocks and never throws.
    void Changed(string deckId);

    // The counts now: null while translations are off; with them on and the deck not live, the primary with a count of 0.
    Task<IReadOnlyList<ReadingInCount>?> CurrentAsync(string deckId);
}
