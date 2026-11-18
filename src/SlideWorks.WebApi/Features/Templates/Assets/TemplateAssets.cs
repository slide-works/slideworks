using System.Text.Json;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Render;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates.Assets;

// A template's files (T6). An upload is two calls, as an image's is: the owner starts it and gets a one-time URL, and
// the bytes are PUT to that URL with no sign-in, because the token in it is the credential.
//
// The token is 256 random bits, kept only as its SHA-256 hash, and lives 10 minutes. It works only while the template's
// owner is still approved. It is used up by one conditional update before a byte of the body is read, so of two PUTs
// racing with it exactly one goes on, and a refused file still uses it up.
//
// Nothing a caller sends reaches the file system: a file's path is its template's id plus the hash of its bytes, and
// the name it was uploaded with is a label on its row. The type a file is served as comes from its kind's check, never
// from the upload. The file arrives in a temp file, counted against its kind's limit while it is copied, and is moved
// into place only after its check has accepted it.
[RegisterPerRequest(For = typeof(ITemplateAssets))]
public class TemplateAssets(
    AppDbContext db,
    ICurrentActor currentActor,
    IPublicUrls urls,
    TemplateAssetStore store,
    AssetChecks checks,
    RenderSessions renderSessions,
    TimeProvider clock) : ITemplateAssets
{
    public const string TEMPLATE_NOT_FOUND = "Template not found";
    public const string UPLOAD_NOT_FOUND = "Upload not found";
    public const string ASSET_NOT_FOUND = "File not found";
    public const string ONLY_A_PERSON = "Only a person can confirm the right to serve a font";
    public const string UPLOAD_ROUTE = "api/template-uploads";

    private const string EXPIRES_LABEL = "in 10 minutes";
    private const string EMPTY_META = "{}";
    private const int TOKEN_MAX_LENGTH = 128;
    private static readonly TimeSpan UPLOAD_LIFETIME = TimeSpan.FromMinutes(10);

    public async Task<AssetUploadStarted> StartUploadAsync(ICurrentActor actor, string templateId, TemplateAssetKind kind, string? fileName, bool rightsConfirmed)
    {
        await CheckOwnerAsync(actor, templateId);
        if (!Enum.IsDefined(kind))
        {
            throw new BadRequestException("kind: must be font, logo, art or layout");
        }
        var label = CheckFileName(fileName);

        var token = SecretTokens.New();
        db.TemplateUploads.Add(new TemplateUpload
        {
            TokenHash = SecretTokens.Hash(token),
            TemplateId = templateId,
            Kind = kind,
            FileName = label,
            // Only a person can say they may serve a font (D68), and the word is about fonts alone.
            RightsConfirmed = rightsConfirmed && kind == TemplateAssetKind.Font && actor.Kind == ActorKind.Person,
            ExpiresAt = clock.GetUtcNow() + UPLOAD_LIFETIME,
        });
        await db.SaveChangesAsync();
        return new AssetUploadStarted($"{urls.Base}/{UPLOAD_ROUTE}/{token}", EXPIRES_LABEL);
    }

    public async Task<StoredAsset> CompleteAsync(string token, Stream content, CancellationToken ct)
    {
        // A token of ours is 43 characters; anything much longer is not one, and is not worth hashing.
        if (token.Length > TOKEN_MAX_LENGTH)
        {
            throw new NotFoundException(UPLOAD_NOT_FOUND);
        }
        var hash = SecretTokens.Hash(token);
        var upload = await ClaimAsync(hash, ct);
        return await CheckAndStoreAsync(upload.TemplateId, upload.Kind, upload.FileName, upload.RightsConfirmed, content, ct);
    }

    public async Task<StoredAsset> StoreAsync(string templateId, TemplateAssetKind kind, string fileName, Stream content, CancellationToken ct)
    {
        if (!await db.Templates.AnyAsync(t => t.Id == templateId, ct))
        {
            throw new NotFoundException(TEMPLATE_NOT_FOUND);
        }
        return await CheckAndStoreAsync(templateId, kind, CheckFileName(fileName), rightsConfirmed: false, content, ct);
    }

    public async Task<AssetFile> OpenAsync(string templateId, string assetId)
    {
        var asset = await db.TemplateAssets.AsNoTracking()
            .Where(a => a.TemplateId == templateId && a.Id == assetId)
            .Select(a => new { a.Kind, a.ContentType })
            .SingleOrDefaultAsync();
        // A layout is read by the server only; the row's ids are now known to be ours, so they may name a path.
        if (asset == null || asset.Kind == TemplateAssetKind.Layout || !await MayReadAsync(templateId) || !store.Exists(templateId, assetId))
        {
            throw new NotFoundException(ASSET_NOT_FOUND);
        }
        return new AssetFile(store.PathFor(templateId, assetId), asset.ContentType, $"\"{assetId}\"");
    }

    public async Task<int> ConfirmFontRightsAsync(ICurrentActor actor, string templateId)
    {
        if (actor.Kind != ActorKind.Person)
        {
            throw new ForbiddenException(ONLY_A_PERSON);
        }
        await CheckOwnerAsync(actor, templateId);
        var fonts = db.TemplateAssets.Where(a => a.TemplateId == templateId && a.Kind == TemplateAssetKind.Font);
        await fonts.Where(a => !a.RightsConfirmed).ExecuteUpdateAsync(set => set.SetProperty(a => a.RightsConfirmed, true));
        return await fonts.CountAsync();
    }

    // The same answer for an unknown template as for someone else's, and for a signed-out caller.
    private async Task CheckOwnerAsync(ICurrentActor actor, string templateId)
    {
        var presenterId = actor.PresenterId;
        if (presenterId == null || !await db.Templates.AnyAsync(t => t.Id == templateId && t.OwnerId == presenterId))
        {
            throw new NotFoundException(TEMPLATE_NOT_FOUND);
        }
    }

    // Takes the token: only the first caller holding it while it is live changes the row, and the checks are part of
    // that one statement. Everyone else learns why from the row as it now is.
    private async Task<TemplateUpload> ClaimAsync(string hash, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var taken = await db.TemplateUploads
            .Where(u => u.TokenHash == hash && u.UsedAt == null && u.ExpiresAt > now
                && db.Templates.Any(t => t.Id == u.TemplateId && db.Users.Any(person => person.Id == t.OwnerId && person.Access == UserAccess.Approved)))
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.UsedAt, now), ct);
        var upload = await db.TemplateUploads.AsNoTracking().SingleOrDefaultAsync(u => u.TokenHash == hash, ct);
        if (taken == 1 && upload != null)
        {
            return upload;
        }
        if (upload?.UsedAt != null)
        {
            throw TemplateRefusal.Of(TemplateErrorCodes.UPLOAD_USED);
        }
        throw new NotFoundException(UPLOAD_NOT_FOUND);
    }

    private async Task<StoredAsset> CheckAndStoreAsync(
        string templateId, TemplateAssetKind kind, string fileName, bool rightsConfirmed, Stream content, CancellationToken ct)
    {
        var check = checks.For(kind);
        var (maxBytes, limit) = LimitOf(kind);
        var temp = await store.WriteTempAsync(content, maxBytes, ct) ?? throw TemplateRefusal.Of(TemplateErrorCodes.FILE_TOO_LARGE, null, ("limit", limit));
        try
        {
            var result = await RunAsync(check, temp, fileName, ct);
            if (!result.Ok)
            {
                throw TemplateRefusal.Of(
                    result.ErrorCode ?? throw new InvalidOperationException($"The {kind} check refused a file without a code."),
                    result.Errors,
                    null,
                    [.. result.ErrorValues]);
            }
            var contentType = result.ContentType ?? throw new InvalidOperationException($"The {kind} check accepted a file without a content type.");
            return await SaveAsync(templateId, kind, fileName, rightsConfirmed, temp, contentType, MetaOf(result.MetaJson) is null ? null : result.MetaJson, ct);
        }
        finally
        {
            File.Delete(temp.Path);
        }
    }

    private static async Task<AssetCheckResult> RunAsync(IAssetCheck check, TempAsset temp, string fileName, CancellationToken ct)
    {
        await using var file = new FileStream(temp.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await check.CheckAsync(file, fileName, ct);
    }

    // One transaction (EF opens it with SQLite's BEGIN IMMEDIATE, so no other write runs beside it): the same bytes
    // already in the template answer the asset that is there, and otherwise the file is moved into place and its row
    // written. If the row fails, a file this call put there goes too, so no file is left without a row by a refusal.
    //
    // A template's files are never removed, so what it may store is capped here, counting every row it has. The count
    // and the new row are in the one write transaction, so two uploads cannot both take the last place. Bytes the
    // template already has are answered before the cap is looked at: they store nothing new.
    private async Task<StoredAsset> SaveAsync(
        string templateId, TemplateAssetKind kind, string fileName, bool rightsConfirmed, TempAsset temp, string contentType, string? metaJson, CancellationToken ct)
    {
        var assetId = temp.Sha256[..TemplateAsset.ID_LENGTH];
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.TemplateAssets.SingleOrDefaultAsync(a => a.TemplateId == templateId && a.Id == assetId, ct);
        if (existing != null)
        {
            // The first upload's row stands, its meta included. The right to serve a font is only ever turned on.
            if (rightsConfirmed && !existing.RightsConfirmed && existing.Kind == TemplateAssetKind.Font)
            {
                existing.RightsConfirmed = true;
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            // Puts the file back if it has gone missing; one that is there is left alone.
            store.Commit(temp.Path, templateId, assetId);
            return ViewOf(existing);
        }
        await CheckRoomAsync(templateId, temp.Size, ct);

        var asset = new TemplateAsset
        {
            Id = assetId,
            TemplateId = templateId,
            Kind = kind,
            FileName = fileName,
            ContentType = contentType,
            Sha256 = temp.Sha256,
            Bytes = temp.Size,
            MetaJson = metaJson,
            RightsConfirmed = rightsConfirmed && kind == TemplateAssetKind.Font,
            CreatedAt = clock.GetUtcNow(),
        };
        var placed = store.Commit(temp.Path, templateId, assetId);
        try
        {
            db.TemplateAssets.Add(asset);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            if (placed)
            {
                store.Delete(templateId, assetId);
            }
            throw;
        }
        return ViewOf(asset);
    }

    private async Task CheckRoomAsync(string templateId, long newBytes, CancellationToken ct)
    {
        var stored = db.TemplateAssets.Where(a => a.TemplateId == templateId);
        if (await stored.CountAsync(ct) >= TemplateLimits.MAX_STORED_ASSETS
            || await stored.SumAsync(a => a.Bytes, ct) + newBytes > TemplateLimits.MAX_STORED_BYTES)
        {
            throw TemplateRefusal.Of(TemplateErrorCodes.TEMPLATE_STORAGE_FULL);
        }
    }

    private async Task<bool> MayReadAsync(string templateId)
    {
        var template = await db.Templates.AsNoTracking()
            .Where(t => t.Id == templateId)
            .Select(t => new { t.OwnerId, t.Access })
            .SingleOrDefaultAsync();
        if (template == null)
        {
            return false;
        }
        if (template.OwnerId == currentActor.PresenterId || template.Access == DeckSharing.Link)
        {
            return true;
        }
        var email = currentActor.IsSignedIn && !string.IsNullOrWhiteSpace(currentActor.Email) ? ViewerEmails.Normalize(currentActor.Email) : null;
        if (template.Access == DeckSharing.Named && email != null && await db.TemplateViewers.AnyAsync(v => v.TemplateId == templateId && v.Email == email))
        {
            return true;
        }
        return await MayViewADeckOnAsync(templateId, email);
    }

    // A deck on the template always renders (D3): whoever may view such a deck may read the template's files. That is
    // the deck's owner, anyone for a deck shared by link, a named viewer, and a headless render page holding a render
    // token for the deck.
    private async Task<bool> MayViewADeckOnAsync(string templateId, string? email)
    {
        var presenterId = currentActor.PresenterId;
        var decks = db.Decks.AsNoTracking().Where(d => d.Template == templateId);
        var shared = await decks.AnyAsync(d =>
            d.Sharing == DeckSharing.Link
            || (presenterId != null && d.OwnerId == presenterId)
            || (email != null && d.Sharing == DeckSharing.Named && db.DeckViewers.Any(v => v.DeckId == d.Id && v.Email == email)));
        return shared || (await decks.Select(d => d.Id).ToListAsync()).Exists(renderSessions.AllowsImagesOf);
    }

    private static StoredAsset ViewOf(TemplateAsset asset)
        => new(asset.Id, asset.Kind, asset.FileName, asset.ContentType, asset.Bytes, MetaOf(asset.MetaJson) ?? MetaOf(EMPTY_META)!.Value);

    // The stored meta as JSON, null when there is none. A check answers an object or nothing.
    private static JsonElement? MetaOf(string? metaJson)
    {
        if (metaJson == null)
        {
            return null;
        }
        using var document = JsonDocument.Parse(metaJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("A file check's meta must be a JSON object.");
        }
        return document.RootElement.Clone();
    }

    private static (long MaxBytes, string Label) LimitOf(TemplateAssetKind kind) => kind switch
    {
        TemplateAssetKind.Font => (TemplateLimits.MAX_FONT_BYTES, "2 MB"),
        TemplateAssetKind.Logo or TemplateAssetKind.Art => (TemplateLimits.MAX_IMAGE_BYTES, "10 MB"),
        TemplateAssetKind.Layout => (TemplateLimits.MAX_LAYOUT_BYTES, "800 KB"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // The name is a label for people: it is stored on the row and never names a file on disk.
    private static string CheckFileName(string? fileName)
    {
        var name = fileName ?? "";
        if (name.Any(char.IsControl))
        {
            throw new BadRequestException("fileName: must not contain control characters");
        }
        var label = name.Trim();
        if (label.Length == 0)
        {
            throw new BadRequestException("fileName: required");
        }
        if (label.Length > TemplateAsset.FILE_NAME_MAX)
        {
            throw new BadRequestException($"fileName: at most {TemplateAsset.FILE_NAME_MAX} characters");
        }
        return label;
    }
}
