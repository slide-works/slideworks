using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// ITemplates over the built-ins and the database (T4). A tpl_ ref reads TemplateVersions on the request's context, with no
// file read, because decks resolve inside write transactions. Finding never asks who is calling: a viewer of a deck gets
// the deck's template. Only the list is by caller.
[RegisterPerRequest(For = typeof(ITemplates))]
public sealed class Templates(ITemplateCatalogue catalogue, AppDbContext db, ILogger<Templates> logger) : ITemplates
{
    public async Task<ResolvedTemplate?> FindAsync(string templateRef, int? version, CancellationToken ct)
    {
        if (!templateRef.StartsWith(TemplateRecord.ID_PREFIX, StringComparison.Ordinal))
        {
            return catalogue.Find(templateRef) is { } builtIn ? BuiltInTemplates.Resolve(builtIn) : null;
        }

        var record = await db.Templates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == templateRef, ct);
        if (record is null)
        {
            return null;
        }
        var published = await db.TemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == templateRef && v.Version > TemplateVersion.DRAFT)
            .OrderByDescending(v => v.Version).ToListAsync(ct);
        var row = version is null ? published.FirstOrDefault() : published.FirstOrDefault(v => v.Version == version);
        if (row is null)
        {
            return null;
        }
        if (ManifestJson.Parse(row.ManifestJson).Manifest is not { } manifest)
        {
            logger.LogWarning("Template {TemplateId} version {Version} has a stored manifest that no longer parses; it is treated as missing", templateRef, row.Version);
            return null;
        }
        var owner = await db.Users.AsNoTracking().Where(u => u.Id == record.OwnerId).Select(u => u.Name).SingleOrDefaultAsync(ct);
        return ManifestTemplates.Resolve(record, row, manifest, published[0].Version, owner);
    }

    public async Task<ResolvedTemplate> ForDeckAsync(Deck deck, CancellationToken ct)
        => await FindAsync(deck.Template, deck.TemplateVersion, ct) ?? BuiltInTemplates.Resolve(catalogue.Default);

    public async Task<IReadOnlyList<ResolvedTemplate>> ListForAsync(ICurrentActor actor, CancellationToken ct)
    {
        var builtIns = catalogue.All.Select(BuiltInTemplates.Resolve);
        if (actor.PresenterId is not { } me)
        {
            return [.. builtIns];
        }

        var email = actor.Email?.ToLowerInvariant();
        var shared = db.TemplateViewers.Where(v => v.Email.ToLower() == email).Select(v => v.TemplateId);
        var records = await db.Templates.AsNoTracking()
            .Where(t => t.ArchivedAt == null
                && (t.OwnerId == me || ((t.Access == DeckSharing.Named || t.Access == DeckSharing.Link) && shared.Contains(t.Id))))
            .ToListAsync(ct);
        var ids = records.Select(r => r.Id).ToList();
        var versions = await db.TemplateVersions.AsNoTracking()
            .Where(v => ids.Contains(v.TemplateId) && v.Version > TemplateVersion.DRAFT).ToListAsync(ct);
        var owners = records.Select(r => r.OwnerId).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => owners.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var uploaded = new List<ResolvedTemplate>();
        foreach (var record in records.OrderByDescending(r => r.OwnerId == me).ThenBy(r => r.CreatedAt).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            var mine = versions.Where(v => v.TemplateId == record.Id).OrderByDescending(v => v.Version).ToList();
            if (mine.Count == 0)
            {
                continue;
            }
            if (ManifestJson.Parse(mine[0].ManifestJson).Manifest is { } manifest)
            {
                uploaded.Add(ManifestTemplates.Resolve(record, mine[0], manifest, mine[0].Version, names.GetValueOrDefault(record.OwnerId)));
            }
            else
            {
                logger.LogWarning("Template {TemplateId} version {Version} has a stored manifest that no longer parses; it is left out of the list", record.Id, mine[0].Version);
            }
        }
        return [.. builtIns, .. uploaded];
    }
}
