using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates.Data;
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
public sealed class TemplatePublishing(AppDbContext db, TimeProvider clock) : ITemplatePublishing
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

    // TODO: returns the screen sample until custom layouts land.
    public Task<LayoutReviewView> LayoutReviewAsync(ICurrentActor actor, string id, string name)
        => Task.FromResult(TemplateSamples.Read<LayoutReviewView>(TemplateSamples.LAYOUT_REVIEW));

    // TODO: accepts the call and changes nothing until custom layouts land.
    public Task ApproveLayoutAsync(ICurrentActor actor, string id, string name, string asset, IReadOnlyList<string> overflow, IReadOnlyList<string> contrast)
        => Task.CompletedTask;

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
            .Select(a => new AssetFact(a.Id, a.Kind, a.RightsConfirmed, a.MetaJson)).ToListAsync();
        return new PublishFacts(manifest, assets);
    }

    private static Guid Me(ICurrentActor actor)
        => actor.PresenterId ?? throw new InvalidOperationException("Templates need a signed-in caller.");
}
