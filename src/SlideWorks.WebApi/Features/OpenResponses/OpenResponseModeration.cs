using Agoda.IoC.Core;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OpenResponses;

// Hide and restore an entry on the presenter's control view (D4, D11). The number is the entry's number on that view; one
// it doesn't list is 404 no_entry, and nothing is written. Hiding a word sets HiddenAt on every row of the round with its
// text key; hiding an answer sets it on that guest's row. A row already in the asked state keeps its HiddenAt, and when no
// row changes nothing is written and nothing is published.
[RegisterPerRequest]
public class OpenResponseModeration(AppDbContext db, OpenResponseControls controls, TimeProvider clock)
{
    public const string NO_ENTRY = "no_entry";

    public Task<OpenResponseControlView> HideAsync(string deckId, int slide, int number) => SetHiddenAsync(deckId, slide, number, hidden: true);

    public Task<OpenResponseControlView> RestoreAsync(string deckId, int slide, int number) => SetHiddenAsync(deckId, slide, number, hidden: false);

    private async Task<OpenResponseControlView> SetHiddenAsync(string deckId, int slide, int number, bool hidden)
    {
        var located = await controls.LocateForPresenterAsync(deckId, slide);
        var entry = OpenResponseControls.EntriesOf(located.Kind, located.Rows).SingleOrDefault(found => found.Entry.Number == number);
        if (entry.Entry == null)
        {
            throw new NotFoundException(NO_ENTRY);
        }
        var changing = entry.Rows.Where(row => (row.HiddenAt != null) != hidden).ToList();
        if (changing.Count == 0)
        {
            return OpenResponseControls.ViewOf(located);
        }
        var now = clock.GetUtcNow();
        changing.ForEach(row => row.HiddenAt = hidden ? now : null);
        located.Current!.Revision++;
        await db.SaveChangesAsync();
        return await controls.AnnounceAsync(located);
    }
}
