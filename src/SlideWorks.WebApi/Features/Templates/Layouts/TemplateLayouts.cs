using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look.CustomLayouts;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Features.Templates.Layouts;

// The custom layouts of a template, read from its manifest's customLayouts and the layout assets those entries name. A
// layout that cannot be read (bad meta, another scope, a schema the compiler no longer accepts) is left out and logged,
// never an error for the caller. Approval is on the asset row and only ever goes from false to true.
[RegisterPerRequest(For = typeof(ITemplateLayouts))]
public sealed class TemplateLayouts(AppDbContext db, ICustomLayoutCompiler compiler, ILogger<TemplateLayouts> log) : ITemplateLayouts
{
    public async Task<IReadOnlyList<TemplateLayout>> ForVersionAsync(string templateId, int version, CancellationToken ct)
    {
        var manifestJson = await db.TemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == templateId && v.Version == version)
            .Select(v => v.ManifestJson)
            .SingleOrDefaultAsync(ct);
        if (manifestJson is null || ManifestJson.Parse(manifestJson).Manifest is not { } manifest || manifest.CustomLayouts.Count == 0)
        {
            return [];
        }

        var ids = manifest.CustomLayouts.Select(entry => entry.File).Distinct(StringComparer.Ordinal).ToList();
        var rows = await db.TemplateAssets.AsNoTracking()
            .Where(a => a.TemplateId == templateId && a.Kind == TemplateAssetKind.Layout && ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, StringComparer.Ordinal, ct);
        var layouts = new List<TemplateLayout>();
        foreach (var entry in manifest.CustomLayouts)
        {
            if (rows.TryGetValue(entry.File, out var row) && Read(row) is { } layout)
            {
                layouts.Add(layout);
            }
        }

        return layouts;
    }

    public async Task<ApproveOutcome> ApproveAsync(string templateId, string name, LayoutReport report, CancellationToken ct)
    {
        // One write transaction (SQLite's BEGIN IMMEDIATE): the draft's entry is read and the row written with no other
        // write between them, so an approval never lands on a layout that was replaced a moment ago.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var manifestJson = await db.TemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == templateId && v.Version == TemplateVersion.DRAFT)
            .Select(v => v.ManifestJson)
            .SingleOrDefaultAsync(ct);
        if (manifestJson is null || ManifestJson.Parse(manifestJson).Manifest is not { } manifest
            || manifest.CustomLayouts.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal)) is not { } entry)
        {
            return ApproveOutcome.NotFound;
        }

        var row = await db.TemplateAssets.SingleOrDefaultAsync(a => a.TemplateId == templateId && a.Kind == TemplateAssetKind.Layout && a.Id == entry.File, ct);
        if (row is null || Read(row) is not { } current)
        {
            return ApproveOutcome.NotFound;
        }

        if (!string.Equals(report.Asset, row.Id, StringComparison.Ordinal))
        {
            return ApproveOutcome.Changed;
        }

        if (report.Overflow.Count > 0)
        {
            return ApproveOutcome.Overflows;
        }

        if (report.Contrast.Count > 0)
        {
            return ApproveOutcome.LowContrast;
        }

        row.MetaJson = new StoredLayout(current.Layout, true, report.Overflow, report.Contrast).Write();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ApproveOutcome.Approved;
    }

    private TemplateLayout? Read(TemplateAsset row)
    {
        try
        {
            var stored = StoredLayout.Read(row.MetaJson, row.Id, compiler);
            return new TemplateLayout(stored.Layout, row.Id, stored.Approved, stored.Overflow, stored.Contrast);
        }
        catch (Exception e) when (e is StoredLayoutRefusedException or JsonException or InvalidOperationException)
        {
            log.LogWarning(e, "A stored custom layout of template {TemplateId} (asset {AssetId}) could not be read and is left out", row.TemplateId, row.Id);
            return null;
        }
    }
}
