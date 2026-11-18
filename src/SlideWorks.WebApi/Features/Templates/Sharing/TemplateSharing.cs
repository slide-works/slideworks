using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Look;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates.Sharing;

// ITemplateSharing over the database (T5), as SharingService is for a deck. Only the owner reads or changes who may use a
// template; anyone else gets the same 404 as for a template that does not exist, so a stranger learns nothing. The
// viewer list is kept whatever the level, so going from named people to the link and back keeps the names; it grants
// access only while the level is Named. A template shared by link is open to anyone on its public page, and a signed-in
// person who presses "Use this template" is remembered as a viewer, so it shows under "Shared with you". Private hides a
// template from everyone but its owner, whatever rows exist. Emails are compared in lower case (ViewerEmails), never with
// SQLite's lower().
[RegisterPerRequest(For = typeof(ITemplateSharing))]
public sealed class TemplateSharing(
    AppDbContext db,
    ITemplates templates,
    IPaletteCatalogue palettes,
    IPublicUrls urls,
    TimeProvider clock) : ITemplateSharing
{
    // The wire names, as for a deck.
    private static readonly IReadOnlyDictionary<string, DeckSharing> LEVELS = new Dictionary<string, DeckSharing>(StringComparer.Ordinal)
    {
        ["private"] = DeckSharing.Private,
        ["link"] = DeckSharing.Link,
        ["named"] = DeckSharing.Named,
    };

    public async Task<TemplateSharingView> GetAsync(ICurrentActor actor, string id)
    {
        var me = Me(actor, id);
        var record = await db.Templates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == me) ?? throw NotFound(id);
        return await ViewAsync(record);
    }

    // A Viewers that is null (set_sharing without viewers) keeps the list as it is; any list replaces it.
    public Task<TemplateSharingView> SetAsync(ICurrentActor actor, string id, TemplateSharingUpdate update)
        => SetAsync(actor, id, update.Level, update.Viewers);

    // The level of a wire name ("private", "link", "named"), matched case-sensitively, for set_sharing.
    public static DeckSharing LevelOf(string? name)
        => name != null && LEVELS.TryGetValue(name, out var known) ? known : throw new BadRequestException(SharingService.LEVEL_INVALID);

    public async Task<TemplatePublicView> PublicAsync(ICurrentActor actor, string id)
    {
        var record = await db.Templates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id) ?? throw NotFound(id);
        var isOwner = actor.PresenterId is { } me && me == record.OwnerId;
        if (!isOwner && (record.ArchivedAt is not null || !await MaySeeAsync(actor, record)))
        {
            throw NotFound(id);
        }

        // Nothing is published yet: there is no page to show, to anyone.
        var template = await templates.FindAsync(id, null, CancellationToken.None) ?? throw NotFound(id);
        var look = LookViews.Default(template, palettes, clock.GetUtcNow());
        return new TemplatePublicView(id, record.Name, template.OwnerName ?? "", template.Template.Purpose, look);
    }

    public async Task UseAsync(ICurrentActor actor, string id)
    {
        var me = Me(actor, id);
        var email = actor.Email is { } own ? ViewerEmails.Normalize(own) : throw NotFound(id);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var record = await db.Templates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id) ?? throw NotFound(id);
        if (record.OwnerId == me)
        {
            return;
        }
        if (record.ArchivedAt is not null || record.Access == DeckSharing.Private)
        {
            throw NotFound(id);
        }
        // Nothing is published yet: there is no page to use, as PublicAsync answers, and no viewer row is written.
        if (!await db.TemplateVersions.AnyAsync(v => v.TemplateId == id && v.Version > TemplateVersion.DRAFT))
        {
            throw NotFound(id);
        }

        var listed = (await db.TemplateViewers.AsNoTracking().Where(v => v.TemplateId == id).Select(v => v.Email).ToListAsync())
            .Any(e => ViewerEmails.Normalize(e) == email);
        if (record.Access == DeckSharing.Named && !listed)
        {
            throw NotFound(id);
        }
        if (!listed)
        {
            db.TemplateViewers.Add(new TemplateViewer { TemplateId = id, Email = email });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
    }

    // The owner's change, in one transaction: the owner check, then every other check, then the first write. A list that
    // is not allowed throws before anything is written. A level or list that changes nothing writes nothing.
    private async Task<TemplateSharingView> SetAsync(ICurrentActor actor, string id, DeckSharing level, IReadOnlyList<string?>? viewers)
    {
        var me = Me(actor, id);
        if (!Enum.IsDefined(level))
        {
            throw new BadRequestException(SharingService.LEVEL_INVALID);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var record = await db.Templates.SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == me) ?? throw NotFound(id);
        var current = await db.TemplateViewers.Where(v => v.TemplateId == id).ToListAsync();
        var next = viewers == null ? null : ViewerEmails.NormalizeAll(viewers, actor.Email);

        var changed = record.Access != level;
        record.Access = level;
        if (next != null)
        {
            // A kept row stored in mixed case (a legacy one) is rewritten in lower case: the email is part of the key, so
            // the old row goes and the lower-case one comes.
            var removed = current.Where(v => !next.Contains(ViewerEmails.Normalize(v.Email), StringComparer.Ordinal) || v.Email != ViewerEmails.Normalize(v.Email)).ToList();
            var added = next.Where(email => current.All(v => v.Email != email)).ToList();
            db.TemplateViewers.RemoveRange(removed);
            db.TemplateViewers.AddRange(added.Select(email => new TemplateViewer { TemplateId = id, Email = email }));
            changed |= removed.Count > 0 || added.Count > 0;
        }
        if (changed)
        {
            record.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        return await ViewAsync(record);
    }

    // Anyone on a link template; the named people (and no one else) on a named one; no one on a private one.
    private async Task<bool> MaySeeAsync(ICurrentActor actor, TemplateRecord record)
    {
        switch (record.Access)
        {
            case DeckSharing.Link:
                return true;
            case DeckSharing.Named when actor.IsSignedIn && actor.Email is { } own:
                var email = ViewerEmails.Normalize(own);
                return (await db.TemplateViewers.AsNoTracking().Where(v => v.TemplateId == record.Id).Select(v => v.Email).ToListAsync())
                    .Any(e => ViewerEmails.Normalize(e) == email);
            default:
                return false;
        }
    }

    private async Task<TemplateSharingView> ViewAsync(TemplateRecord record)
    {
        var viewers = await db.TemplateViewers.AsNoTracking().Where(v => v.TemplateId == record.Id).Select(v => v.Email).ToListAsync();
        return new TemplateSharingView(record.Access, [.. viewers.Select(ViewerEmails.Normalize).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)], urls.Base + "/t/" + record.Id);
    }

    private static Guid Me(ICurrentActor actor, string id) => actor.PresenterId ?? throw NotFound(id);

    private static NotFoundException NotFound(string id) => new($"No template '{id}'.");
}
