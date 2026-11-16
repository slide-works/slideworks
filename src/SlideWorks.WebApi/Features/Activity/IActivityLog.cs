namespace SlideWorks.WebApi.Features.Activity;

// A deck's change log (K7). Every change records exactly one entry. The only writer of ActivityEntryRow.
public interface IActivityLog
{
    // Adds the entry, stamped with the time from TimeProvider, and saves it in the caller's open transaction if there is one.
    Task RecordAsync(string deckId, ActivityEntry entry);

    // The deck's newest entries first, at most max of them.
    Task<IReadOnlyList<ActivityEntryRow>> ListAsync(string deckId, int max);
}
