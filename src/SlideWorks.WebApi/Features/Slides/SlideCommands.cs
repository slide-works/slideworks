using System.Text.Json;
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
    TimeProvider clock)
{
    public const string ADD_TOOL = "add_slide";

    private const int NOTES_MAX_LENGTH = 4000;

    public async Task<AddSlideResult> AddAsync(AddSlideRequest request)
    {
        if (!await access.CanEditAsync(request.Deck, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var validation = SlideValidator.Validate(request.Layout, request.Slots);
        if (!validation.IsValid)
        {
            throw new BadRequestException(validation.Error);
        }
        foreach (var imageId in validation.ImageIds)
        {
            if (!await images.ExistsAsync(request.Deck, imageId))
            {
                throw new BadRequestException($"{ImagePath(request.Layout, request.Slots, imageId)}: unknown image {imageId}");
            }
        }
        if (request.Notes is { Length: > NOTES_MAX_LENGTH })
        {
            throw new BadRequestException($"notes: at most {NOTES_MAX_LENGTH} characters");
        }

        Slide slide;
        Deck deck;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            // Taking the deck's next revision first also takes SQLite's write lock, so two adds to one deck run one at a time.
            await db.Decks.Where(d => d.Id == request.Deck)
                .ExecuteUpdateAsync(set => set.SetProperty(d => d.Revision, d => d.Revision + 1).SetProperty(d => d.UpdatedAt, clock.GetUtcNow()));
            deck = await db.Decks.AsNoTracking().SingleAsync(d => d.Id == request.Deck);
            CheckPart(request.Part, DeckParts.Read(deck).Count);
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
                SlotsJson = SlideJson.ForStorage(request.Slots),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes,
                Steps = SlideSteps.Count(request.Layout, request.Slots),
                Revision = deck.Revision,
            };
            db.Slides.Add(slide);
            await db.SaveChangesAsync();
            await activity.RecordAsync(deck.Id, ActivityEntry.By(actor, ADD_TOOL, $"added slide {position + 1} ({slide.Layout})"));
            await transaction.CommitAsync();
        }

        await events.PublishAsync(new DeckChanged(deck.Id));
        var number = slide.Position + 1;
        return new AddSlideResult(number, slide.Steps, slide.Revision, urls.Deck(deck.Id, deck.Slug, number));
    }

    private static void CheckPart(int? part, int parts)
    {
        if (part == null || (part >= 1 && part <= parts))
        {
            return;
        }
        throw new BadRequestException(parts == 0 ? "part: the deck has no parts" : $"part: must be between 1 and {parts}");
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

    // The path of the image slot that holds imageId, in the validator's form: "image.image", "two-column.left.image".
    private static string ImagePath(string layout, JsonElement slots, string imageId)
        => FindImage(slots, layout, imageId) ?? $"{layout}.image";

    private static string? FindImage(JsonElement element, string path, string imageId)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Select((item, index) => FindImage(item, $"{path}[{index}]", imageId)).FirstOrDefault(found => found != null);
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("image", out var id) && id.ValueKind == JsonValueKind.String && id.ValueEquals(imageId)
                && property.Value.TryGetProperty("alt", out _))
            {
                return $"{path}.{property.Name}";
            }
            var found = FindImage(property.Value, $"{path}.{property.Name}", imageId);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
