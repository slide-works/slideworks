using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Slides;

// Adds slides (K5), for the add_slide tool. Agents change slides through MCP only in SW-1 (D15).
[RegisterPerRequest]
public class SlideCommands(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IDeckImages images,
    IActivityLog activity,
    IDeckEvents events,
    IPublicUrls urls,
    ITemplateCatalogue templates,
    PollRows pollRows,
    TimeProvider clock)
{
    public const string ADD_TOOL = "add_slide";

    public async Task<AddSlideResult> AddAsync(AddSlideRequest request)
    {
        if (!await access.CanEditAsync(request.Deck, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var slots = request.Slots;
        await SlideChecks.ValidateAsync(images, request.Deck, request.Layout, slots);
        if (request.Notes is { Length: > SlideChecks.NOTES_MAX_LENGTH })
        {
            throw new BadRequestException($"notes: at most {SlideChecks.NOTES_MAX_LENGTH} characters");
        }

        Slide slide;
        Deck deck;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            // EF opens a SQLite transaction with BEGIN IMMEDIATE, which takes the database's write lock at once, so writes run
            // one at a time. Taking the deck's next revision first is the convention; it isn't what serialises them.
            await db.Decks.Where(d => d.Id == request.Deck)
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.Revision, d => d.Revision + 1).SetProperty(d => d.UpdatedAt, clock.GetUtcNow()));
            deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == request.Deck);
            SlideChecks.CheckPart(request.Part, DeckParts.Read(deck).Count);
            var count = await db.Slides.CountAsync(s => s.DeckId == deck.Id && s.DeletedAt == null);
            var position = Position(request.Position, count);

            // Every later slide moves down one and is stamped with the new revision, so an old slide number plus an old
            // revision can never match a moved slide (K5).
            await db.Slides.Where(s => s.DeckId == deck.Id && s.DeletedAt == null && s.Position >= position)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.Position, s => s.Position + 1).SetProperty(s => s.Revision, deck.Revision));
            slide = new Slide
            {
                Id = Guid.NewGuid(),
                DeckId = deck.Id,
                Position = position,
                Layout = request.Layout,
                Part = request.Part,
                SlotsJson = SlideJson.ForStorage(slots),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes,
                Steps = SlideSteps.Count(request.Layout, slots),
                Revision = deck.Revision,
                DeepDive = request.DeepDive ?? false,
            };
            db.Slides.Add(slide);
            await db.SaveChangesAsync();
            if (slide.Layout == PollRows.LAYOUT)
            {
                await pollRows.EnsureAsync(slide.Id, deck.Id);
            }
            await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, ADD_TOOL, $"added slide {position + 1} ({slide.Layout}){(slide.DeepDive ? " as a deep dive" : "")}"));
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deck.Id));
        var number = slide.Position + 1;
        var warnings = LayoutWarnings.For(LookViews.TemplateOf(deck, templates), number, slide.Layout);
        return new AddSlideResult(number, slide.Steps, slide.Revision, urls.Deck(deck.Id, deck.Slug, number), warnings);
    }

    // The 0-based position for a 1-based number, which may be one past the last slide.
    private static int Position(int? number, int count)
    {
        if (number == null)
        {
            return count;
        }
        if (number < 1 || number > count + 1)
        {
            throw new BadRequestException($"position: must be between 1 and {count + 1}");
        }
        return number.Value - 1;
    }
}
