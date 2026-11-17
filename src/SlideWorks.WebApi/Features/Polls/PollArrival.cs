using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Polls;

// The arrival rule (V4, D2, D12): what the presenter reaching a slide does to polls. "Leaving" is worked out from the
// session's unclosed round against the slide reached, never from a remembered previous slide, because presentation states
// can arrive out of order. One read answers everything the no-change paths need: the deck's open session, its unclosed
// round's slide and whether it was opened, and the slide reached with its layout, its opens setting and whether it has any
// round in the session. Then:
//   - not live, or the unclosed round is on the slide reached: nothing;
//   - a round really open on another slide: IPolls.CloseAsync on that slide;
//   - a poll with opens onArrival and no round row in this session: IPolls.OpenAsync, which discards a draft elsewhere first.
// A draft is never touched here: it stays until Open or New round on another slide, or the end of the session, discards it.
// A refusal from IPolls (ConflictException: arrival raced a presenter's button or another arrival) and a slide that stopped
// being a poll under it (NotFoundException) are expected and logged; the next state settles it. Anything else is thrown to
// the observer. Calls IPolls only; IPolls knows nothing of this. Per request, because it uses the database.
[RegisterPerRequest(For = typeof(IPollArrival))]
public class PollArrival(AppDbContext db, IPolls polls, ILogger<PollArrival> logger) : IPollArrival
{
    public async Task ArrivedAsync(string deckId, int slide)
    {
        var position = slide - 1;
        var facts = await db.AudienceSessions.AsNoTracking()
            .Where(s => s.DeckId == deckId && s.EndedAt == null)
            .Select(s => new
            {
                SessionId = s.Id,
                UnclosedSlideId = db.PollRounds.Where(r => r.SessionId == s.Id && r.ClosedAt == null).Select(r => (Guid?)r.SlideId).FirstOrDefault(),
                UnclosedOpened = db.PollRounds.Where(r => r.SessionId == s.Id && r.ClosedAt == null).Any(r => r.OpenedAt != null),
                SlideId = db.Slides.Where(x => x.DeckId == deckId && x.DeletedAt == null && x.Position == position).Select(x => (Guid?)x.Id).FirstOrDefault(),
                Layout = db.Slides.Where(x => x.DeckId == deckId && x.DeletedAt == null && x.Position == position).Select(x => x.Layout).FirstOrDefault(),
            })
            .Select(f => new
            {
                f.SessionId,
                f.UnclosedSlideId,
                f.UnclosedOpened,
                f.SlideId,
                f.Layout,
                Opens = db.Polls.Where(p => p.SlideId == f.SlideId).Select(p => (PollOpens?)p.Opens).FirstOrDefault(),
                HasRound = db.PollRounds.Any(r => r.SlideId == f.SlideId && r.SessionId == f.SessionId),
            })
            .SingleOrDefaultAsync();

        if (facts == null || (facts.UnclosedSlideId != null && facts.UnclosedSlideId == facts.SlideId))
        {
            return;
        }
        if (facts.UnclosedSlideId is { } left && facts.UnclosedOpened)
        {
            await CloseLeftAsync(deckId, left);
        }
        if (facts.Layout == PollRows.LAYOUT && facts.Opens == PollOpens.OnArrival && !facts.HasRound)
        {
            await TryAsync(deckId, slide, "open", () => polls.OpenAsync(deckId, slide));
        }
    }

    // The round left behind is closed by its slide's number now. A slide deleted while its round was open has no number, so
    // its round stays open until the session ends (Polls.EndSessionAsync closes it).
    private async Task CloseLeftAsync(string deckId, Guid slideId)
    {
        var left = await db.Slides.AsNoTracking().Where(s => s.Id == slideId && s.DeletedAt == null).Select(s => (int?)s.Position).SingleOrDefaultAsync();
        if (left is not { } leftPosition)
        {
            logger.LogWarning("Arrival left the open poll round on deleted slide {SlideId} of deck {DeckId} open", slideId, deckId);
            return;
        }
        await TryAsync(deckId, leftPosition + 1, "close", () => polls.CloseAsync(deckId, leftPosition + 1));
    }

    private async Task TryAsync(string deckId, int slide, string action, Func<Task> act)
    {
        try
        {
            await act();
        }
        catch (ConflictException refused)
        {
            logger.LogInformation(refused, "Arrival's {Action} of the poll on slide {Slide} of deck {DeckId} was refused: {Code}", action, slide, deckId, refused.Message);
        }
        catch (NotFoundException missing)
        {
            logger.LogInformation(missing, "Arrival's {Action} found no poll on slide {Slide} of deck {DeckId}", action, slide, deckId);
        }
    }
}
