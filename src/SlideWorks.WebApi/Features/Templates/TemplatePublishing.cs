using Agoda.IoC.Core;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Layouts;
using SlideWorks.WebApi.Features.Templates.Publishing;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// ITemplatePublishing over the database (T5). Publishing copies the draft into the next version number in one
// transaction (EF opens it with SQLite's BEGIN IMMEDIATE) after every checklist row has passed, so a refused publish
// leaves nothing behind. A published version is never written again. Archiving only sets ArchivedAt: nothing is deleted
// and the decks on the template keep rendering. A template that is not the actor's own is NotFoundException.
[RegisterPerRequest(For = typeof(ITemplatePublishing))]
public sealed partial class TemplatePublishing(AppDbContext db, TimeProvider clock, ITemplateLayouts layouts, ICustomLayoutCompiler compiler, ILogger<TemplatePublishing> logger)
    : ITemplatePublishing
{
    public async Task<TemplateChecklistView> ChecklistAsync(ICurrentActor actor, string id)
    {
        var me = Me(actor);
        _ = await db.Templates.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == me)
            ?? throw new NotFoundException($"No template '{id}'.");
        var rows = PublishChecks.Run(await FactsAsync(id, noTracking: true));
        return new TemplateChecklistView([.. rows.Select(r => r.Row)], rows.All(r => !r.Blocks));
    }

    public async Task<TemplatePublished> PublishAsync(ICurrentActor actor, string id)
    {
        var me = Me(actor);
        await using var transaction = await db.Database.BeginTransactionAsync();
        _ = await db.Templates.SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == me)
            ?? throw new NotFoundException($"No template '{id}'.");

        var facts = await FactsAsync(id, noTracking: false);
        var blocked = PublishChecks.Run(facts).FirstOrDefault(r => r.Blocks);
        if (blocked is not null)
        {
            throw new TemplateRefusedException(blocked.Code!, blocked.Message!, blocked.Path);
        }

        var highest = await db.TemplateVersions.Where(v => v.TemplateId == id).MaxAsync(v => v.Version);
        var version = highest + 1;
        db.TemplateVersions.Add(new TemplateVersion
        {
            TemplateId = id,
            Version = version,
            ManifestJson = ManifestJson.Write(facts.Manifest),
            FitWidthsJson = FontWidths.Json(facts),
            PublishedAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();

        var behind = await db.Decks.CountAsync(d => d.OwnerId == me && d.Template == id && d.TemplateVersion != null && d.TemplateVersion < version);
        await transaction.CommitAsync();
        return new TemplatePublished(version, behind);
    }

    public async Task ArchiveAsync(ICurrentActor actor, string id)
    {
        var me = Me(actor);
        var record = await db.Templates.SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == me)
            ?? throw new NotFoundException($"No template '{id}'.");
        if (record.ArchivedAt is null)
        {
            record.ArchivedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
        }
    }

    public async Task<LayoutReviewView> LayoutReviewAsync(ICurrentActor actor, string id, string name)
    {
        await OwnTemplateAsync(actor, id);
        var layout = (await layouts.ForVersionAsync(id, TemplateVersion.DRAFT, CancellationToken.None))
            .FirstOrDefault(l => string.Equals(l.Layout.Name, name, StringComparison.Ordinal))
            ?? throw new NotFoundException($"No layout '{name}'.");
        return LayoutReviews.ViewOf(layout);
    }

    public async Task ApproveLayoutAsync(ICurrentActor actor, string id, string name, string asset, IReadOnlyList<string> overflow, IReadOnlyList<string> contrast)
    {
        await OwnTemplateAsync(actor, id);
        var outcome = await layouts.ApproveAsync(id, name, new LayoutReport(asset, overflow, contrast), CancellationToken.None);
        switch (outcome)
        {
            case ApproveOutcome.Approved:
                LogLayoutApproved(id, name, asset, Me(actor));
                return;
            case ApproveOutcome.NotFound:
                throw new NotFoundException($"No layout '{name}'.");
            case ApproveOutcome.Archived:
                throw TemplateRefusal.Of(TemplateErrorCodes.TEMPLATE_ARCHIVED);
            case ApproveOutcome.Changed:
                throw new ConflictException(TemplateErrorCodes.LAYOUT_CHANGED);
            case ApproveOutcome.Overflows:
                throw TemplateRefusal.Of(TemplateErrorCodes.LAYOUT_OVERFLOWS, Findings(overflow, TemplateErrorCodes.LAYOUT_OVERFLOWS), name);
            case ApproveOutcome.LowContrast:
                throw TemplateRefusal.Of(TemplateErrorCodes.LAYOUT_LOW_CONTRAST, Findings(contrast, TemplateErrorCodes.LAYOUT_LOW_CONTRAST), name);
            default:
                throw new InvalidOperationException($"Unknown approval outcome {outcome}.");
        }
    }

    // The owner's finding names are text the owner's page sent: the refusal's text is the code's, and the findings are
    // listed under it with that same text.
    private static IReadOnlyList<TemplateRefusalDetail> Findings(IReadOnlyList<string> findings, string code)
        => [.. findings.Select(finding => new TemplateRefusalDetail(finding, code, TemplateErrorTexts.Format(code)))];

    private async Task OwnTemplateAsync(ICurrentActor actor, string id)
    {
        var me = Me(actor);
        if (!await db.Templates.AsNoTracking().AnyAsync(t => t.Id == id && t.OwnerId == me))
        {
            throw new NotFoundException($"No template '{id}'.");
        }
    }

    // What the editor shows is what is checked: the draft, or the latest published version when there is no draft row. A
    // stored manifest that no longer parses, or a template with no version row, is a refusal and never a 500.
    private async Task<PublishFacts> FactsAsync(string id, bool noTracking)
    {
        var versions = db.TemplateVersions.Where(v => v.TemplateId == id);
        var rows = await (noTracking ? versions.AsNoTracking() : versions).OrderBy(v => v.Version).ToListAsync();
        var current = rows.FirstOrDefault(v => v.Version == TemplateVersion.DRAFT) ?? rows.LastOrDefault()
            ?? throw TemplateRefusal.Of(TemplateErrorCodes.MANIFEST_INVALID, null, ("reason", "the template has no draft"));
        var parsed = ManifestJson.Parse(current.ManifestJson);
        if (parsed.Manifest is not { } manifest)
        {
            var error = parsed.Errors[0];
            throw TemplateRefusal.Of(TemplateErrorCodes.MANIFEST_INVALID, error.Path.Length == 0 ? null : error.Path, ("reason", error.Message));
        }

        var assets = await db.TemplateAssets.AsNoTracking().Where(a => a.TemplateId == id)
            .Select(a => new { a.Id, a.Kind, a.RightsConfirmed, a.MetaJson }).ToListAsync();
        return new PublishFacts(manifest, [.. assets.Select(a => new AssetFact(a.Id, a.Kind, a.RightsConfirmed, a.MetaJson, LayoutOf(a.Id, a.Kind, a.MetaJson)))]);
    }

    // A layout file's name and approval, read the way every reader reads one; a layout that cannot be read has none, and the
    // layouts check then refuses it as not found.
    private LayoutFact? LayoutOf(string assetId, TemplateAssetKind kind, string? metaJson)
    {
        if (kind != TemplateAssetKind.Layout)
        {
            return null;
        }

        try
        {
            var stored = StoredLayout.Read(metaJson, assetId, compiler);
            return new LayoutFact(stored.Layout.Name, stored.Approved);
        }
        catch (Exception e) when (e is StoredLayoutRefusedException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(e, "A stored custom layout (asset {AssetId}) could not be read for the publish checks", assetId);
            return null;
        }
    }

    // Approval is of the draft, so there is no version yet; the asset is the layout's stored file.
    [LoggerMessage(Level = LogLevel.Information, Message = "Custom layout {LayoutName} (asset {AssetId}) of template {TemplateId} approved by account {AccountId}")]
    private partial void LogLayoutApproved(string templateId, string layoutName, string assetId, Guid accountId);

    private static Guid Me(ICurrentActor actor)
        => actor.PresenterId ?? throw new InvalidOperationException("Templates need a signed-in caller.");
}
