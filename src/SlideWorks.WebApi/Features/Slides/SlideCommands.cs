using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Polls;
using SlideWorks.Core.Slides;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Polls;
using SlideWorks.WebApi.Features.Polls.Data;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates;
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
    ITemplates templates,
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
        // Shape and layout, before any write. A rule that needs the option count waits until the slots have passed.
        var change = PollChange(request.Layout, request.Poll);
        var slots = request.Slots;

        Slide slide;
        Deck deck;
        ResolvedTemplate template;
        PollSettings? settings = null;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            // EF opens a SQLite transaction with BEGIN IMMEDIATE, which takes the database's write lock at once, so writes run
            // one at a time. Taking the deck's next revision first is the convention; it isn't what serialises them.
            await db.Decks.Where(d => d.Id == request.Deck)
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.Revision, d => d.Revision + 1).SetProperty(d => d.UpdatedAt, clock.GetUtcNow()));
            deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == request.Deck);

            // Checked inside the transaction, against the template the deck has now. A refusal rolls the revision back.
            template = await templates.ForDeckAsync(deck, CancellationToken.None);
            var definition = await SlideChecks.ValidateAsync(images, deck.Id, template, request.Layout, slots, request.Surface);
            if (request.Notes is { Length: > SlideChecks.NOTES_MAX_LENGTH })
            {
                throw new BadRequestException($"notes: at most {SlideChecks.NOTES_MAX_LENGTH} characters");
            }
            SlideChecks.CheckPart(request.Part, DeckParts.Read(deck).Count);
            settings = request.Layout == PollRows.LAYOUT ? SettingsForNewPoll(change, slots) : null;
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
                Steps = SlideSteps.Count(definition, slots),
                Revision = deck.Revision,
                DeepDive = request.DeepDive ?? false,
                Surface = request.Surface,
            };
            db.Slides.Add(slide);
            await db.SaveChangesAsync();
            if (slide.Layout == PollRows.LAYOUT)
            {
                await pollRows.EnsureAsync(slide.Id, deck.Id, settings);
            }
            await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, ADD_TOOL, $"added slide {position + 1} ({slide.Layout}){(slide.DeepDive ? " as a deep dive" : "")}"));
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deck.Id));
        var number = slide.Position + 1;
        var warnings = LayoutWarnings.For(template.Template, number, slide.Layout);
        return new AddSlideResult(number, slide.Steps, slide.Revision, urls.Deck(deck.Id, deck.Slug, number), warnings, settings);
    }

    public const string ADD_MANY_TOOL = "add_slides";

    // The slides that fit are stored in the given order, at one position. A slide that does not fit is reported
    // and left out: it does not move the others and it does not take a number. Nothing is written when none fit.
    public async Task<AddSlidesResult> AddManyAsync(string deckId, IReadOnlyList<SlideDraft?>? drafts, int? position)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        if (drafts is null || drafts.Count == 0)
        {
            throw new BadRequestException("slides: at least 1");
        }
        if (drafts.Any(draft => draft is null))
        {
            throw new BadRequestException("slides: an entry is missing");
        }

        var deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deckId);
        var template = await templates.ForDeckAsync(deck, CancellationToken.None);
        var partCount = DeckParts.Read(deck).Count;
        var prepared = new List<PreparedSlide>(drafts.Count);
        for (var index = 0; index < drafts.Count; index++)
        {
            prepared.Add(await PrepareAsync(deck.Id, template, partCount, index, drafts[index]!));
        }

        var accepted = prepared.Where(item => item.Error is null).ToList();
        if (accepted.Count == 0)
        {
            return new AddSlidesResult(deck.Revision, 0, prepared.Select(item => item.Result()).ToList());
        }

        var count = await db.Slides.CountAsync(s => s.DeckId == deck.Id && s.DeletedAt == null);
        var start = Position(position, count);

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Decks.Where(d => d.Id == deck.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.Revision, d => d.Revision + 1).SetProperty(d => d.UpdatedAt, clock.GetUtcNow()));
        deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == deck.Id);
        count = await db.Slides.CountAsync(s => s.DeckId == deck.Id && s.DeletedAt == null);
        start = Position(position, count);
        await db.Slides.Where(s => s.DeckId == deck.Id && s.DeletedAt == null && s.Position >= start)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Position, s => s.Position + accepted.Count).SetProperty(s => s.Revision, deck.Revision));

        var number = start + 1;
        foreach (var item in accepted)
        {
            var slide = new Slide
            {
                Id = Guid.NewGuid(),
                DeckId = deck.Id,
                Position = number - 1,
                Layout = item.Draft.Layout,
                Part = item.Draft.Part,
                SlotsJson = SlideJson.ForStorage(item.Draft.Slots),
                Notes = string.IsNullOrWhiteSpace(item.Draft.Notes) ? null : item.Draft.Notes,
                Steps = item.Steps,
                Revision = deck.Revision,
                DeepDive = item.Draft.DeepDive ?? false,
                Surface = item.Draft.Surface,
            };
            db.Slides.Add(slide);
            if (slide.Layout == PollRows.LAYOUT)
            {
                await db.SaveChangesAsync();
                await pollRows.EnsureAsync(slide.Id, deck.Id, item.Settings);
            }

            item.Stored(number, deck.Revision, urls.Deck(deck.Id, deck.Slug, number), LayoutWarnings.For(template.Template, number, slide.Layout));
            number++;
        }

        await db.SaveChangesAsync();
        await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, ADD_MANY_TOOL, $"added {accepted.Count} slides at {start + 1}"));
        await transaction.CommitAsync();
        await events.PublishAsync(new DeckChanged(deck.Id));
        return new AddSlidesResult(deck.Revision, accepted.Count, prepared.Select(item => item.Result()).ToList());
    }

    private async Task<PreparedSlide> PrepareAsync(string deckId, ResolvedTemplate template, int partCount, int index, SlideDraft draft)
    {
        try
        {
            var change = PollChange(draft.Layout, draft.Poll);
            SlideChecks.CheckPart(draft.Part, partCount);
            if (draft.Notes is { Length: > SlideChecks.NOTES_MAX_LENGTH })
            {
                throw new BadRequestException($"notes: at most {SlideChecks.NOTES_MAX_LENGTH} characters");
            }

            var (definition, result) = await SlideChecks.ReportAsync(images, deckId, template, draft.Layout, draft.Slots, draft.Surface);
            if (!result.IsValid)
            {
                return new PreparedSlide(index, draft, 0, result.Regions, result.Error);
            }

            var settings = draft.Layout == PollRows.LAYOUT ? SettingsForNewPoll(change, draft.Slots) : null;
            return new PreparedSlide(index, draft, SlideSteps.Count(definition, draft.Slots), result.Regions, settings: settings);
        }
        catch (BadRequestException exception)
        {
            return new PreparedSlide(index, draft, 0, [], exception.Message);
        }
    }

    // The poll object, or null when the entry leaves it out. A poll object on any other layout, or a poll that is not an
    // object, is a slide error. The rules that need the option count run only after the slots have passed.
    private static PollSettingsChange? PollChange(string layout, JsonElement? poll)
    {
        var change = PollSettingsCommands.ReadOptional(poll);
        if (change is not null && layout != PollRows.LAYOUT)
        {
            throw new BadRequestException(PollSettingsCommands.ONLY_POLL_LAYOUT);
        }
        return change;
    }

    private static PollSettings SettingsForNewPoll(PollSettingsChange? change, JsonElement slots)
        => PollSettingsCommands.ForNewPoll(change, PollReader.SlotsOf(SlideJson.ForStorage(slots)).Options.Count);

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

    private sealed class PreparedSlide
    {
        private readonly int index;
        private readonly int steps;
        private readonly IReadOnlyList<FitRegion> fit;
        private readonly string? error;
        private int? slide;
        private long? revision;
        private string? url;
        private IReadOnlyList<string> warnings = [];

        public PreparedSlide(int index, SlideDraft draft, int steps, IReadOnlyList<FitRegion> fit, string? error = null, PollSettings? settings = null)
        {
            this.index = index;
            Draft = draft;
            this.steps = steps;
            this.fit = fit;
            this.error = error;
            Settings = settings;
        }

        public SlideDraft Draft { get; }

        public PollSettings? Settings { get; }

        public string? Error => error;

        public int Steps => steps;

        public void Stored(int number, long storedRevision, string storedUrl, IReadOnlyList<string> storedWarnings)
        {
            slide = number;
            revision = storedRevision;
            url = storedUrl;
            warnings = storedWarnings;
        }

        public AddSlidesItemResult Result() => new(
            index,
            error is null,
            slide,
            error is null ? steps : null,
            revision,
            url,
            warnings,
            error,
            fit,
            error is null ? Settings : null);
    }
}
