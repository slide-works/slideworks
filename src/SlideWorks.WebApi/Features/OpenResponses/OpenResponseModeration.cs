using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.OpenResponses;

// Hide and restore an entry on the presenter's control view (D4, D11). The number is the entry's number on that view; one
// it doesn't list is 404 no_entry, and nothing is written.
[RegisterPerRequest]
public class OpenResponseModeration(OpenResponseControls controls)
{
    public const string NO_ENTRY = "no_entry";

    public async Task<OpenResponseControlView> HideAsync(string deckId, int slide, int number)
    {
        var view = await RequireEntryAsync(deckId, slide, number);
        // TODO: set HiddenAt on every row of the round with that entry's text key, then read the view back.
        return Flagged(view, number, hidden: true);
    }

    public async Task<OpenResponseControlView> RestoreAsync(string deckId, int slide, int number)
    {
        var view = await RequireEntryAsync(deckId, slide, number);
        // TODO: clear HiddenAt on every row of the round with that entry's text key, then read the view back.
        return Flagged(view, number, hidden: false);
    }

    private async Task<OpenResponseControlView> RequireEntryAsync(string deckId, int slide, int number)
    {
        var view = await controls.ViewAsync(deckId, slide);
        return view.Entries.Any(entry => entry.Number == number) ? view : throw new NotFoundException(NO_ENTRY);
    }

    private static OpenResponseControlView Flagged(OpenResponseControlView view, int number, bool hidden) =>
        view with { Entries = [.. view.Entries.Select(entry => entry.Number == number ? entry with { Hidden = hidden } : entry)] };
}
