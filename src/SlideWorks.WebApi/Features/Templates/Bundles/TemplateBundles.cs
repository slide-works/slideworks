using System.IO.Compression;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Features.Templates.Drafts;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates.Bundles;

// ITemplateBundles over the drafts and the asset store (D4, T5).
//
// Import copies the body to a temp file (capped), opens it as a zip and checks everything before it writes anything:
// the entries' names and headers, the manifest, that the manifest names exactly the files the bundle holds, and every
// file through its kind's IAssetCheck. Only then does it write, in this order: the draft, each file through
// ITemplateAssets.StoreAsync, the manifest with the files' asset ids. Nothing is ever written to a path built from an
// entry's name; the temp file is deleted in every case.
//
// Export writes the template's current manifest and its files into a temp file that is deleted when it is closed.
[RegisterPerRequest(For = typeof(ITemplateBundles))]
public sealed class TemplateBundles(
    ITemplateDrafts drafts,
    ITemplateAssets assets,
    ITemplates templates,
    AssetChecks checks,
    TemplateAssetStore store,
    AppDbContext db,
    TimeProvider clock) : ITemplateBundles
{
    // A new record's revision (TemplateRecord.Revision).
    private const int NEW_DRAFT_REVISION = 1;
    private const string FONTS_FOLDER = "fonts/";
    private const string LAYOUTS_FOLDER = "layouts/";

    public async Task<string> ImportAsync(ICurrentActor actor, Stream zip, CancellationToken ct)
    {
        var temp = await store.WriteTempAsync(zip, TemplateLimits.MAX_BUNDLE_BYTES, ct)
            ?? throw TemplateRefusal.Of(TemplateErrorCodes.BUNDLE_TOO_LARGE);
        try
        {
            await using var file = new FileStream(temp.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            using var archive = OpenZip(file);
            var bundle = BundleArchive.Open(archive);
            var (manifest, files) = await PlanAsync(bundle, ct);
            return await WriteAsync(actor, manifest, files, ct);
        }
        finally
        {
            File.Delete(temp.Path);
        }
    }

    public async Task<TemplateBundleFile> ExportAsync(ICurrentActor actor, string id, CancellationToken ct)
    {
        var me = actor.PresenterId ?? throw new InvalidOperationException("Templates need a signed-in caller.");
        var owned = await db.Templates.AsNoTracking().AnyAsync(t => t.Id == id && t.OwnerId == me, ct);
        if (!owned)
        {
            throw new NotFoundException($"No template '{id}'.");
        }
        var versions = new VersionSet([.. await db.TemplateVersions.AsNoTracking().Where(v => v.TemplateId == id).OrderBy(v => v.Version).ToListAsync(ct)]);
        var manifest = ManifestJson.Parse(versions.Current.ManifestJson).Manifest
            ?? throw new InvalidOperationException($"The stored manifest of {id} no longer parses.");
        var rows = await db.TemplateAssets.AsNoTracking().Where(a => a.TemplateId == id).ToDictionaryAsync(a => a.Id, StringComparer.Ordinal, ct);

        var output = new FileStream(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            await WriteBundleAsync(output, id, manifest, rows, ct);
            output.Position = 0;
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
        return new TemplateBundleFile(output, BundleNames.DownloadName(manifest.Name));
    }

    private static ZipArchive OpenZip(FileStream file)
    {
        try
        {
            return new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            throw BundleArchive.Invalid("it is not a zip file");
        }
    }

    // Everything an import refuses, checked without writing: the manifest, the files it names against the files the
    // bundle holds, and each file's own check.
    private async Task<(TemplateManifest Manifest, IReadOnlyList<BundleFile> Files)> PlanAsync(BundleArchive bundle, CancellationToken ct)
    {
        if (!bundle.Has(BundleNames.MANIFEST))
        {
            throw BundleArchive.Invalid($"{BundleNames.MANIFEST} is missing");
        }
        var parsed = ManifestJson.Parse(System.Text.Encoding.UTF8.GetString(await bundle.ReadAsync(BundleNames.MANIFEST, ManifestJson.MAX_BYTES, ct)));
        if (parsed.Manifest is not { } manifest)
        {
            var error = parsed.Errors[0];
            throw BundleArchive.Invalid($"{BundleNames.MANIFEST} {error.Path} {error.Message}".Replace("  ", " ", StringComparison.Ordinal), error.Path.Length == 0 ? null : error.Path);
        }
        if (manifest.Label.Length > TemplateRecord.NAME_MAX)
        {
            throw BundleArchive.Invalid($"the label is longer than {TemplateRecord.NAME_MAX} characters", "label");
        }
        if (await templates.FindAsync(manifest.BasedOn, null, ct) is not { BuiltIn: true })
        {
            throw BundleArchive.Invalid("basedOn names no built-in template", "basedOn");
        }

        var files = Files(bundle, manifest);
        var unnamed = bundle.Paths.Where(p => p != BundleNames.MANIFEST && !files.Any(f => Holds(f, p))).ToList();
        if (unnamed.Count > 0)
        {
            throw BundleArchive.Invalid("it holds a file that template.json does not name");
        }
        // Content-addressed storage keeps one row, and so one kind, per distinct content (a normal upload of the same bytes
        // as a second kind answers the first row too), so the same bytes used as two kinds are refused here, not after
        // the draft exists.
        var seen = new Dictionary<string, BundleFile>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var hash = await CheckAsync(file, ct);
            if (seen.TryGetValue(hash, out var first) && first.Kind != file.Kind)
            {
                throw BundleArchive.Invalid($"it holds the same content as {first.Path}, which is used as another kind of file", file.Path);
            }
            seen.TryAdd(hash, file);
        }
        // Each file is read again to be stored; its bytes were already counted against the bundle's total.
        bundle.RestartCount();
        return (manifest, files);
    }

    // One entry per distinct path the manifest names, each of one kind.
    private static List<BundleFile> Files(BundleArchive bundle, TemplateManifest manifest)
    {
        var files = new List<BundleFile>();
        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var reference in ManifestFiles.Refs(manifest))
        {
            if (!BundleNames.IsPlainPath(reference.Value))
            {
                throw BundleArchive.Invalid("a file path in template.json is not a relative path of plain names", reference.Path);
            }
            if (kinds.TryGetValue(reference.Value, out var known))
            {
                if (known != reference.Kind)
                {
                    throw BundleArchive.Invalid("template.json names one file for two kinds of use", reference.Path);
                }
                continue;
            }
            kinds[reference.Value] = reference.Kind;
            files.Add(reference.Kind == ManifestFiles.LAYOUT ? LayoutFile(bundle, reference) : SingleFile(bundle, reference));
        }
        return files;
    }

    private static BundleFile SingleFile(BundleArchive bundle, ManifestFileRef reference)
    {
        if (!bundle.Has(reference.Value))
        {
            throw BundleArchive.Invalid("template.json names a file the bundle does not hold", reference.Path);
        }
        var kind = reference.Kind switch
        {
            ManifestFiles.FONT => TemplateAssetKind.Font,
            ManifestFiles.LOGO => TemplateAssetKind.Logo,
            _ => TemplateAssetKind.Art,
        };
        var limit = kind == TemplateAssetKind.Font ? TemplateLimits.MAX_FONT_BYTES : TemplateLimits.MAX_IMAGE_BYTES;
        var name = reference.Value[(reference.Value.LastIndexOf('/') + 1)..];
        return new BundleFile(reference.Value, kind, Label(name), ct => bundle.ReadAsync(reference.Value, limit, ct));
    }

    private static BundleFile LayoutFile(BundleArchive bundle, ManifestFileRef reference)
    {
        var stem = reference.Value;
        if (new[] { LayoutDocument.HTML, LayoutDocument.CSS, LayoutDocument.JSON }.FirstOrDefault(part => !bundle.Has(stem + part)) is { } missing)
        {
            throw BundleArchive.Invalid($"layout {stem} is missing its {missing} file", reference.Path);
        }
        return new BundleFile(stem, TemplateAssetKind.Layout, Label(stem[(stem.LastIndexOf('/') + 1)..] + LayoutDocument.JSON), async ct =>
        {
            var html = await bundle.ReadAsync(stem + LayoutDocument.HTML, TemplateLimits.MAX_LAYOUT_PART_BYTES, ct);
            var css = await bundle.ReadAsync(stem + LayoutDocument.CSS, TemplateLimits.MAX_LAYOUT_PART_BYTES, ct);
            var slots = await bundle.ReadAsync(stem + LayoutDocument.JSON, TemplateLimits.MAX_LAYOUT_PART_BYTES, ct);
            var document = LayoutDocument.Of(stem, html, css, slots).ToBytes();
            return document.Length > TemplateLimits.MAX_LAYOUT_BYTES ? throw TemplateRefusal.Of(TemplateErrorCodes.BUNDLE_TOO_LARGE) : document;
        });
    }

    private static bool Holds(BundleFile file, string entry)
        => file.Kind == TemplateAssetKind.Layout
            ? entry == file.Path + LayoutDocument.HTML || entry == file.Path + LayoutDocument.CSS || entry == file.Path + LayoutDocument.JSON
            : entry == file.Path;

    private static string Label(string name) => name.Length > TemplateAsset.FILE_NAME_MAX ? name[..TemplateAsset.FILE_NAME_MAX] : name;

    // The file through its kind's check: a refusal carries the check's code, findings and the manifest path's file.
    // Answers the SHA-256 of the bytes that would be stored.
    private async Task<string> CheckAsync(BundleFile file, CancellationToken ct)
    {
        try
        {
            TemplateAssets.CheckFileName(file.FileName);
        }
        catch (BadRequestException)
        {
            throw BundleArchive.Invalid("the file's name is empty once its spaces are removed", file.Path);
        }
        var bytes = await file.Load(ct);
        await using var content = new MemoryStream(bytes, writable: false);
        var result = await checks.For(file.Kind).CheckAsync(content, file.FileName, ct);
        if (!result.Ok)
        {
            throw TemplateRefusal.Of(
                result.ErrorCode ?? throw new InvalidOperationException($"The {file.Kind} check refused a file without a code."),
                result.Errors,
                file.Path,
                [.. result.ErrorValues]);
        }
        return Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    // The writes, after every refusal. An unexpected failure from here (a disk error) is not a refusal and can leave a
    // part-filled draft.
    private async Task<string> WriteAsync(ICurrentActor actor, TemplateManifest manifest, IReadOnlyList<BundleFile> files, CancellationToken ct)
    {
        var created = await drafts.CreateAsync(actor, manifest.BasedOn, manifest.Label);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            await using var content = new MemoryStream(await file.Load(ct), writable: false);
            ids[file.Path] = (await assets.StoreAsync(created.Id, file.Kind, file.FileName, content, ct)).AssetId;
        }
        await drafts.UpdateAsync(actor, created.Id, ManifestJson.Write(ManifestFiles.With(manifest, ids)), NEW_DRAFT_REVISION);
        return created.Id;
    }

    private async Task WriteBundleAsync(
        Stream output, string templateId, TemplateManifest manifest, IReadOnlyDictionary<string, TemplateAsset> rows, CancellationToken ct)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var reference in ManifestFiles.Refs(manifest))
        {
            if (!rows.TryGetValue(reference.Value, out var row))
            {
                throw new InvalidOperationException($"Template {templateId} names a file {reference.Value} that it does not hold.");
            }
            if (paths.ContainsKey(reference.Value))
            {
                continue;
            }
            var stem = reference.Kind == ManifestFiles.LAYOUT ? LAYOUTS_FOLDER + LayoutName(manifest, reference) : null;
            var path = stem ?? ExportPath(reference, row);
            paths[reference.Value] = path;
            if (stem is null)
            {
                await using var source = new FileStream(store.PathFor(templateId, row.Id), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                await using var entry = await Entry(zip, path).OpenAsync(ct);
                await source.CopyToAsync(entry, ct);
            }
            else
            {
                var document = LayoutDocument.Read(await File.ReadAllBytesAsync(store.PathFor(templateId, row.Id), ct));
                await WriteTextAsync(Entry(zip, stem + LayoutDocument.HTML), System.Text.Encoding.UTF8.GetBytes(document.Markup), ct);
                await WriteTextAsync(Entry(zip, stem + LayoutDocument.CSS), System.Text.Encoding.UTF8.GetBytes(document.Css), ct);
                await WriteTextAsync(Entry(zip, stem + LayoutDocument.JSON), System.Text.Encoding.UTF8.GetBytes(document.Slots.GetRawText()), ct);
            }
        }
        await WriteTextAsync(Entry(zip, BundleNames.MANIFEST), System.Text.Encoding.UTF8.GetBytes(ManifestJson.Write(ManifestFiles.With(manifest, paths))), ct);
        await output.FlushAsync(ct);
    }

    private ZipArchiveEntry Entry(ZipArchive zip, string path)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = clock.GetUtcNow();
        return entry;
    }

    private static async Task WriteTextAsync(ZipArchiveEntry entry, byte[] bytes, CancellationToken ct)
    {
        await using var stream = await entry.OpenAsync(ct);
        await stream.WriteAsync(bytes, ct);
    }

    // The layout's stem name comes from the manifest entry whose file is this one.
    private static string LayoutName(TemplateManifest manifest, ManifestFileRef reference)
        => manifest.CustomLayouts.First(layout => layout.File == reference.Value).Name;

    // Where a file goes in the bundle. Never the stored label, which is free text: a font is named by its asset id, a
    // logo and the art by their role, with the extension its content type gives.
    private static string ExportPath(ManifestFileRef reference, TemplateAsset row)
    {
        var extension = row.ContentType switch
        {
            AssetContentTypes.WOFF2 => ".woff2",
            AssetContentTypes.SVG => ".svg",
            AssetContentTypes.PNG => ".png",
            AssetContentTypes.WEBP => ".webp",
            _ => ".bin",
        };
        return reference.Path switch
        {
            "logos.light" => "logo-light" + extension,
            "logos.dark" => "logo-dark" + extension,
            "titleArt.file" => "art" + extension,
            _ => FONTS_FOLDER + row.Id + extension,
        };
    }
}
