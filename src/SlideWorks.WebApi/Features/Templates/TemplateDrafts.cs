using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Drafts;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// ITemplateDrafts over the database (T5): a person's templates are TemplateRecord rows and their draft is the version 0
// row. Every write runs in one transaction (EF opens it with SQLite's BEGIN IMMEDIATE, which takes the write lock at
// once), and every check runs before the first write, so a refused change leaves nothing behind. A template that is not
// the actor's own is NotFoundException, whether or not it exists.
[RegisterPerRequest(For = typeof(ITemplateDrafts))]
public sealed class TemplateDrafts(
    AppDbContext db,
    ITemplates templates,
    ITemplatePublishing publishing,
    IPaletteCatalogue palettes,
    TimeProvider clock) : ITemplateDrafts
{
    public async Task<TemplatesView> ListAsync(ICurrentActor actor)
    {
        var me = Me(actor);
        var now = clock.GetUtcNow();
        var pickable = await templates.ListForAsync(actor, CancellationToken.None);
        var builtIns = pickable.Where(t => t.BuiltIn).ToList();
        var others = pickable.Where(t => !t.BuiltIn && t.OwnerId != me).ToList();

        var records = await db.Templates.AsNoTracking().Where(t => t.OwnerId == me).OrderByDescending(t => t.UpdatedAt).ToListAsync();
        var otherIds = others.Select(t => t.Ref).ToList();
        var sharedRecords = await db.Templates.AsNoTracking().Where(t => otherIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        var versions = await VersionsOfAsync([.. records.Select(r => r.Id)]);
        var counts = await DeckCountsAsync([.. records.Select(r => r.Id), .. otherIds, .. builtIns.Select(t => t.Ref)]);

        var mine = records.Select(record =>
        {
            var own = versions[record.Id];
            return TemplateRows.OfManifest(record, Parsed(own.Current.ManifestJson), own.Published, counts.GetValueOrDefault(record.Id), now);
        }).ToList();
        var shared = others
            .Where(t => sharedRecords.ContainsKey(t.Ref))
            .Select(t => TemplateRows.OfResolved(sharedRecords[t.Ref], t, palettes, counts.GetValueOrDefault(t.Ref), now))
            .ToList();
        var builtIn = builtIns.Select(t => TemplateRows.BuiltIn(t, palettes, counts.GetValueOrDefault(t.Ref), now)).ToList();
        return new TemplatesView(mine, shared, builtIn);
    }

    public async Task<TemplateCreated> CreateAsync(ICurrentActor actor, string basedOn, string name)
    {
        var me = Me(actor);
        var builtIn = await templates.FindAsync(basedOn, null, CancellationToken.None);
        if (builtIn is not { BuiltIn: true })
        {
            throw new NotFoundException($"No built-in template named '{basedOn}'.");
        }
        var label = name.Trim();
        var manifest = ManifestChecked(DraftManifests.StartingFrom(builtIn, label), label);

        await using var transaction = await db.Database.BeginTransactionAsync();
        var owned = await db.Templates.CountAsync(t => t.OwnerId == me && t.ArchivedAt == null);
        if (owned >= TemplateLimits.MAX_TEMPLATES_PER_PERSON)
        {
            throw TemplateRefusal.Of(TemplateErrorCodes.TOO_MANY_TEMPLATES);
        }
        var now = clock.GetUtcNow();
        var record = new TemplateRecord
        {
            Id = TemplateRecord.ID_PREFIX + Decks.DeckIds.New(),
            OwnerId = me,
            Name = label,
            Access = Decks.DeckSharing.Private,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Templates.Add(record);
        db.TemplateVersions.Add(new TemplateVersion { TemplateId = record.Id, Version = TemplateVersion.DRAFT, ManifestJson = manifest });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new TemplateCreated(record.Id);
    }

    public async Task<TemplateEditorView> GetAsync(ICurrentActor actor, string id)
    {
        var me = Me(actor);
        var record = await db.Templates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == me)
            ?? throw new NotFoundException($"No template '{id}'.");
        var versions = (await VersionsOfAsync([id]))[id];
        var assets = await db.TemplateAssets.AsNoTracking().Where(a => a.TemplateId == id).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync();
        var checklist = await publishing.ChecklistAsync(actor, id);
        return new TemplateEditorView(
            record.Id,
            record.Name,
            (int)record.Revision,
            TemplateRows.StatusOf(record, versions.Published),
            versions.Published,
            TemplateEditorViews.Json(versions.Current.ManifestJson),
            [.. assets.Select(TemplateEditorViews.Asset)],
            checklist.Checklist,
            [.. versions.All.Where(v => v.Version > TemplateVersion.DRAFT).Select(TemplateEditorViews.Published)],
            checklist.CanPublish);
    }

    public async Task<TemplateEditorView> UpdateAsync(ICurrentActor actor, string id, string manifestJson, int revision)
    {
        var me = Me(actor);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var record = await db.Templates.SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == me)
                ?? throw new NotFoundException($"No template '{id}'.");
            if (record.Revision != revision)
            {
                throw TemplateRefusal.Of(TemplateErrorCodes.STALE_REVISION);
            }
            var parsed = ManifestJson.Parse(manifestJson);
            if (parsed.Manifest is not { } manifest)
            {
                throw Invalid(parsed.Errors[0]);
            }
            if (manifest.Label.Length > TemplateRecord.NAME_MAX)
            {
                throw Invalid(new ManifestError("label", $"must be at most {TemplateRecord.NAME_MAX} characters"));
            }
            await CheckFilesAsync(id, manifest);

            var draft = await db.TemplateVersions.SingleOrDefaultAsync(v => v.TemplateId == id && v.Version == TemplateVersion.DRAFT);
            var stored = ManifestJson.Write(manifest);
            if (draft is null)
            {
                db.TemplateVersions.Add(new TemplateVersion { TemplateId = id, Version = TemplateVersion.DRAFT, ManifestJson = stored });
            }
            else
            {
                draft.ManifestJson = stored;
            }
            record.Name = manifest.Label;
            record.Revision++;
            record.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        db.ChangeTracker.Clear();
        return await GetAsync(actor, id);
    }

    // Every file field names an asset of this template, of the kind the field takes.
    private async Task CheckFilesAsync(string id, TemplateManifest manifest)
    {
        var assets = await db.TemplateAssets.AsNoTracking().Where(a => a.TemplateId == id).Select(a => new { a.Id, a.Kind }).ToListAsync();
        var kinds = assets.ToDictionary(a => a.Id, a => a.Kind, StringComparer.Ordinal);
        foreach (var file in ManifestFiles.Refs(manifest))
        {
            if (!kinds.TryGetValue(file.Value, out var kind) || kind != KindOf(file.Kind))
            {
                throw TemplateRefusal.Of(TemplateErrorCodes.ASSET_NOT_FOUND, file.Path);
            }
        }
    }

    private static TemplateAssetKind KindOf(string fileKind) => fileKind switch
    {
        ManifestFiles.FONT => TemplateAssetKind.Font,
        ManifestFiles.LOGO => TemplateAssetKind.Logo,
        ManifestFiles.ART => TemplateAssetKind.Art,
        ManifestFiles.LAYOUT => TemplateAssetKind.Layout,
        _ => throw new ArgumentOutOfRangeException(nameof(fileKind), fileKind, "Not a manifest file kind."),
    };

    // The text a new draft stores: the manifest as ManifestJson writes it, after it has read it back without a finding.
    private static string ManifestChecked(TemplateManifest manifest, string label)
    {
        if (label.Length > TemplateRecord.NAME_MAX)
        {
            throw Invalid(new ManifestError("name", $"must be at most {TemplateRecord.NAME_MAX} characters"));
        }
        var text = ManifestJson.Write(manifest);
        var parsed = ManifestJson.Parse(text);
        return parsed.Manifest is null ? throw Invalid(parsed.Errors[0]) : text;
    }

    private static TemplateRefusedException Invalid(ManifestError error)
        => TemplateRefusal.Of(TemplateErrorCodes.MANIFEST_INVALID, error.Path.Length == 0 ? null : error.Path, ("reason", error.Message));

    private static Guid Me(ICurrentActor actor)
        => actor.PresenterId ?? throw new InvalidOperationException("Templates need a signed-in caller.");

    private static TemplateManifest Parsed(string json)
        => ManifestJson.Parse(json).Manifest ?? throw new InvalidOperationException("A stored template manifest no longer parses.");

    private async Task<Dictionary<string, VersionSet>> VersionsOfAsync(IReadOnlyList<string> ids)
    {
        var rows = await db.TemplateVersions.AsNoTracking().Where(v => ids.Contains(v.TemplateId)).ToListAsync();
        return ids.ToDictionary(id => id, id => new VersionSet([.. rows.Where(v => v.TemplateId == id).OrderBy(v => v.Version)]));
    }

    private async Task<Dictionary<string, int>> DeckCountsAsync(IReadOnlyList<string> ids)
        => await db.Decks.AsNoTracking().Where(d => ids.Contains(d.Template)).GroupBy(d => d.Template)
            .Select(g => new { Ref = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Ref, g => g.Count);
}
