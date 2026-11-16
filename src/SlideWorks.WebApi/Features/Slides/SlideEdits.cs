using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Slides;

// Changes to slides that already exist (K5, K6): update_slide, move_slide, delete_slide and set_notes. Each one runs in
// one transaction (EF opens it with SQLite's BEGIN IMMEDIATE, which is what makes writes run one at a time) that first
// takes the deck's next revision; stamps every slide it changes or renumbers with that revision; records one activity entry (K7); and publishes one DeckChanged (K9).
// Slide numbers are 1-based among the live slides, and live positions stay 0..n-1 with no gaps.
[RegisterPerRequest]
public class SlideEdits(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IDeckImages images,
    IActivityLog activity,
    IDeckEvents events,
    IPublicUrls urls,
    TimeProvider clock)
{
    public const string UPDATE_TOOL = "update_slide";
    public const string MOVE_TOOL = "move_slide";
    public const string DELETE_TOOL = "delete_slide";
    public const string SET_NOTES_TOOL = "set_notes";

    // Refused with a conflict when the slide at that number no longer carries the revision the caller read.
    public Task<UpdateSlideResult> UpdateAsync(UpdateSlideRequest request)
        => EditAsync(request.Deck, UPDATE_TOOL, async deck =>
        {
            CheckNumber(request.Slide, await Live(deck.Id).CountAsync(), "slide");
            var slide = await Live(deck.Id).SingleAsync(s => s.Position == request.Slide - 1);
            if (slide.Revision != request.Revision)
            {
                throw new ConflictException($"Slide {request.Slide} changed since revision {request.Revision}; read it again with get_deck");
            }
            var layout = request.Layout ?? slide.Layout;
            var slots = SlideChecks.RequireSlots(request.Slots);
            await SlideChecks.ValidateAsync(images, deck.Id, layout, slots);
            SlideChecks.CheckPart(request.Part, DeckParts.Read(deck).Count);

            slide.Layout = layout;
            slide.SlotsJson = SlideJson.ForStorage(slots);
            slide.Steps = SlideSteps.Count(layout, slots);
            slide.Part = request.Part ?? slide.Part;
            slide.Revision = deck.Revision;
            await db.SaveChangesAsync();
            var result = new UpdateSlideResult(request.Slide, slide.Steps, slide.Revision, urls.Deck(deck.Id, deck.Slug, request.Slide));
            return (result, $"updated slide {request.Slide} ({layout})");
        });

    // The slides between the old and the new place shift by one toward the gap, and they and the moved slide get the new
    // revision, so an old number plus an old revision never matches a moved slide.
    public Task<MoveSlideResult> MoveAsync(string deckId, int slide, int to)
        => EditAsync(deckId, MOVE_TOOL, async deck =>
        {
            var count = await Live(deck.Id).CountAsync();
            CheckNumber(slide, count, "slide");
            CheckNumber(to, count, "to");
            int from = slide - 1, target = to - 1;
            var moved = await Live(deck.Id).Where(s => s.Position == from).Select(s => s.Id).SingleAsync();
            if (target < from)
            {
                await Live(deck.Id).Where(s => s.Position >= target && s.Position < from)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.Position, s => s.Position + 1).SetProperty(s => s.Revision, deck.Revision));
            }
            else if (target > from)
            {
                await Live(deck.Id).Where(s => s.Position > from && s.Position <= target)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.Position, s => s.Position - 1).SetProperty(s => s.Revision, deck.Revision));
            }
            await db.Slides.Where(s => s.Id == moved)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.Position, target).SetProperty(s => s.Revision, deck.Revision));
            return (new MoveSlideResult(to, deck.Revision, urls.Deck(deck.Id, deck.Slug, to)), $"moved slide {slide} to {to}");
        });

    // A soft delete: the slide is kept with DeletedAt set. Every later slide moves up one to close the gap and gets the new
    // revision.
    public Task<DeleteSlideResult> DeleteAsync(string deckId, int slide)
        => EditAsync(deckId, DELETE_TOOL, async deck =>
        {
            var count = await Live(deck.Id).CountAsync();
            CheckNumber(slide, count, "slide");
            var position = slide - 1;
            var deleted = await Live(deck.Id).SingleAsync(s => s.Position == position);
            deleted.DeletedAt = clock.GetUtcNow();
            deleted.Revision = deck.Revision;
            await db.SaveChangesAsync();
            await Live(deck.Id).Where(s => s.Position > position)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.Position, s => s.Position - 1).SetProperty(s => s.Revision, deck.Revision));
            return (new DeleteSlideResult(slide, count - 1, deck.Revision), $"deleted slide {slide} ({deleted.Layout})");
        });

    // Sets the notes of every slide listed, as one change with one activity entry. The tool passes notes and its entries
    // as it bound them, so a missing list or a null entry is refused here with a message naming it.
    public Task<SetNotesResult> SetNotesAsync(string deckId, IReadOnlyList<SlideNote?>? given)
        => EditAsync(deckId, SET_NOTES_TOOL, async deck =>
        {
            var notes = CheckNotes(given, await Live(deck.Id).CountAsync());
            var positions = notes.Select(note => note.Slide - 1).ToList();
            var slides = await Live(deck.Id).Where(s => positions.Contains(s.Position)).ToDictionaryAsync(s => s.Position);
            foreach (var note in notes)
            {
                var slide = slides[note.Slide - 1];
                slide.Notes = string.IsNullOrWhiteSpace(note.Text) ? null : note.Text;
                slide.Revision = deck.Revision;
            }
            await db.SaveChangesAsync();
            var numbers = notes.Select(note => note.Slide).ToList();
            var summary = numbers.Count == 1 ? $"set notes on slide {numbers[0]}" : $"set notes on slides {string.Join(", ", numbers)}";
            return (new SetNotesResult(numbers, deck.Revision), summary);
        });

    private async Task<T> EditAsync<T>(string deckId, string tool, Func<Deck, Task<(T Result, string Summary)>> edit)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }

        T result;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            // BEGIN IMMEDIATE has already taken the database's write lock; taking the deck's next revision first is the
            // convention, not what serialises the edits. A refused edit throws before the commit, so the revision it took
            // is rolled back with everything else.
            await db.Decks.Where(d => d.Id == deckId)
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.Revision, d => d.Revision + 1).SetProperty(d => d.UpdatedAt, clock.GetUtcNow()));
            var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);
            (result, var summary) = await edit(deck);
            await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, tool, summary));
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deckId));
        return result;
    }

    private IQueryable<Slide> Live(string deckId) => db.Slides.Where(s => s.DeckId == deckId && s.DeletedAt == null);

    private static void CheckNumber(int number, int count, string field)
    {
        if (number >= 1 && number <= count)
        {
            return;
        }
        throw new BadRequestException(count == 0 ? $"{field}: the deck has no slides" : $"{field}: must be between 1 and {count}");
    }

    private static IReadOnlyList<SlideNote> CheckNotes(IReadOnlyList<SlideNote?>? notes, int count)
    {
        if (notes == null)
        {
            throw new BadRequestException("notes: required");
        }
        if (notes.Count == 0)
        {
            throw new BadRequestException("notes: list at least one slide");
        }
        var seen = new HashSet<int>();
        var checkedNotes = new List<SlideNote>(notes.Count);
        for (var index = 0; index < notes.Count; index++)
        {
            var note = notes[index] ?? throw new BadRequestException($"notes[{index}]: must be an object with slide and text");
            checkedNotes.Add(note);
            CheckNumber(note.Slide, count, $"notes[{index}].slide");
            if (!seen.Add(note.Slide))
            {
                throw new BadRequestException($"notes[{index}].slide: slide {note.Slide} is listed twice");
            }
            if (note.Text is { Length: > SlideChecks.NOTES_MAX_LENGTH })
            {
                throw new BadRequestException($"notes[{index}].text: at most {SlideChecks.NOTES_MAX_LENGTH} characters");
            }
        }
        return checkedNotes;
    }
}
