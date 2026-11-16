using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Sharing.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Sharing;

// Who can open a deck (K4 sharing.json, D6): GET and PUT /api/decks/{id}/sharing for the owner in the browser, and the
// set_sharing tool for the owner's agent, under the same rules. Anyone but the owner gets 404 "Deck not found". A change
// runs in one transaction that first stamps Deck.UpdatedAt from TimeProvider (which also takes SQLite's write lock),
// replaces the viewer list, records exactly one activity entry ("changed sharing: Private → Anyone with the link") and
// then publishes one DeckChanged. A request that changes nothing records nothing.
//
// The viewer list is kept whatever the level, so switching from named people to the link and back keeps the names; it
// grants access only while the level is Named (DeckAccess).
[RegisterPerRequest]
public class SharingService(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IActivityLog activity,
    IDeckEvents events,
    IPublicUrls urls,
    TimeProvider clock)
{
    public const string TOOL = "set_sharing";

    public const string LEVEL_INVALID = "level: must be \"private\", \"link\" or \"named\"";

    public const string VIEWERS_INVALID = "viewers: must be a list of email addresses";

    private const string SUMMARY_PREFIX = "changed sharing: ";

    // The wire names (camelCase, as every enum goes on the wire), matched case-sensitively.
    private static readonly IReadOnlyDictionary<string, DeckSharing> LEVELS = new Dictionary<string, DeckSharing>(StringComparer.Ordinal)
    {
        ["private"] = DeckSharing.Private,
        ["link"] = DeckSharing.Link,
        ["named"] = DeckSharing.Named,
    };

    public async Task<SharingView> GetAsync(string deckId)
    {
        await RequireOwnerAsync(deckId);
        return await ViewAsync(deckId);
    }

    // The dialog's PUT: the body carries the level and the whole list.
    public async Task<SharingView> UpdateAsync(string deckId, SharingUpdate update)
    {
        await RequireOwnerAsync(deckId);
        return await SetAsync(deckId, update.Level, ViewerEmails.NormalizeAll(update.Viewers), tool: null);
    }

    // set_sharing: the level by its wire name; leaving viewers out keeps the list as it is.
    public async Task<SharingView> SetFromAgentAsync(string deckId, string? level, IReadOnlyList<string?>? viewers)
    {
        await RequireOwnerAsync(deckId);
        var parsed = level != null && LEVELS.TryGetValue(level, out var known) ? known : throw new BadRequestException(LEVEL_INVALID);
        return await SetAsync(deckId, parsed, viewers == null ? null : ViewerEmails.NormalizeAll(viewers), TOOL);
    }

    private async Task<SharingView> SetAsync(string deckId, DeckSharing level, IReadOnlyList<string>? viewers, string? tool)
    {
        var changed = false;
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Decks.Where(d => d.Id == deckId).ExecuteUpdateAsync(set => set.SetProperty(d => d.UpdatedAt, clock.GetUtcNow()));
            var from = await db.Decks.Where(d => d.Id == deckId).Select(d => d.Sharing).SingleAsync();
            var current = await db.DeckViewers.Where(v => v.DeckId == deckId).Select(v => v.Email).ToListAsync();
            var next = viewers ?? current;
            var summary = Summary(from, level, current, next);
            if (summary != null)
            {
                await db.Decks.Where(d => d.Id == deckId).ExecuteUpdateAsync(set => set.SetProperty(d => d.Sharing, level));
                await db.DeckViewers.Where(v => v.DeckId == deckId && !next.Contains(v.Email)).ExecuteDeleteAsync();
                db.DeckViewers.AddRange(next.Except(current, StringComparer.Ordinal).Select(email => new DeckViewer { DeckId = deckId, Email = email }));
                await db.SaveChangesAsync();
                await activity.RecordAsync(deckId, ActivityEntry.By(actor, tool, summary));
                await transaction.CommitAsync();
                changed = true;
            }
        }

        if (changed)
        {
            await events.PublishAsync(new DeckChanged(deckId));
        }
        return await ViewAsync(deckId);
    }

    // What the change log says, or null when nothing changed.
    private static string? Summary(DeckSharing from, DeckSharing to, IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var added = after.Except(before, StringComparer.Ordinal).ToList();
        var removed = before.Except(after, StringComparer.Ordinal).ToList();
        var viewers = added.Count + removed.Count == 0
            ? ""
            : $" ({string.Join(", ", added.Select(e => "+" + e).Concat(removed.Select(e => "-" + e)))})";
        if (from != to)
        {
            return $"{SUMMARY_PREFIX}{Label(from)} → {Label(to)}{viewers}";
        }
        return viewers.Length == 0 ? null : $"{SUMMARY_PREFIX}{Label(to)}{viewers}";
    }

    // The dialog's words for each level.
    private static string Label(DeckSharing level) => level switch
    {
        DeckSharing.Private => "Private",
        DeckSharing.Link => "Anyone with the link",
        DeckSharing.Named => "Named people",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    private async Task<SharingView> ViewAsync(string deckId)
    {
        var deck = await db.Decks.AsNoTracking().Where(d => d.Id == deckId).Select(d => new { d.Id, d.Slug, d.Sharing }).SingleAsync();
        var viewers = await db.DeckViewers.AsNoTracking().Where(v => v.DeckId == deckId).Select(v => v.Email).OrderBy(e => e).ToListAsync();
        var last = await db.ActivityEntries.AsNoTracking()
            .Where(e => e.DeckId == deckId && e.Summary.StartsWith(SUMMARY_PREFIX))
            .OrderByDescending(e => e.At)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync();
        var lastChange = last == null ? null : SharingLastChange.From(last, DisplayDate.Ago(last.At, clock.GetUtcNow()));
        return new SharingView(deck.Sharing, viewers, urls.Deck(deck.Id, deck.Slug), lastChange);
    }

    private async Task RequireOwnerAsync(string deckId)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
    }
}
