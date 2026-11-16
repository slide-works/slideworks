using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Render;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Images;

// Images on a deck (K11): upload_image makes an image and a one-time upload URL, PUT /api/uploads/{token} brings its
// bytes, and GET /api/images/{id} serves them to whoever may view the deck.
//
// The upload token is 256 random bits, stored only as its SHA-256 hash (SecretTokens), and valid for 10 minutes. It works
// only while the deck's owner is still approved, so rejecting or revoking a presenter stops their outstanding uploads. It
// is consumed by one conditional update, so of two PUTs racing with it exactly one stores the image and the other gets 404. A body that is
// too big or isn't the declared type is refused before the token is consumed, so the agent can PUT the right bytes again.
[RegisterPerRequest]
public class ImageUploads(
    AppDbContext db,
    ICurrentActor actor,
    IDeckAccess access,
    IActivityLog activity,
    IDeckEvents events,
    IPublicUrls urls,
    ImageStore store,
    RenderSessions renderSessions,
    TimeProvider clock)
{
    public const string TOOL = "upload_image";
    public const string UPLOAD_NOT_FOUND = "Upload not found";
    public const string IMAGE_NOT_FOUND = "Image not found";

    private const int NAME_MAX_LENGTH = 200;
    private const string ID_PREFIX = "img_";
    private const int ID_RANDOM_LENGTH = 10;
    private const string ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly TimeSpan UPLOAD_LIFETIME = TimeSpan.FromMinutes(10);

    // Owner only: anyone else, like an unknown deck, gets "Deck not found". name is a label for people (the change log
    // says "started uploading story-map.png", because the upload may never complete); it never reaches a file path.
    public async Task<UploadImageResult> CreateAsync(string deckId, string name, string contentType)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var label = CheckName(name);
        var type = ImageTypes.Normalise(contentType) ?? throw new BadRequestException($"contentType: must be {ImageTypes.ACCEPTED_LIST}");

        var token = SecretTokens.New();
        var image = new DeckImage
        {
            Id = ID_PREFIX + RandomNumberGenerator.GetString(ALPHABET, ID_RANDOM_LENGTH),
            DeckId = deckId,
            Name = label,
            ContentType = type,
            UploadTokenHash = SecretTokens.Hash(token),
            UploadExpiresAt = clock.GetUtcNow() + UPLOAD_LIFETIME,
        };
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.DeckImages.Add(image);
            await db.SaveChangesAsync();
            await activity.RecordAsync(deckId, ActivityEntry.By(actor, TOOL, $"started uploading {label}"));
            await transaction.CommitAsync();
        }
        await events.PublishAsync(new DeckChanged(deckId));
        return new UploadImageResult(image.Id, $"{urls.Base}/api/uploads/{token}", "PUT", (int)UPLOAD_LIFETIME.TotalSeconds);
    }

    // 404 for a token that is unknown, used or expired, or whose deck's owner is no longer approved; 413 over 10 MB; 400 when the bytes aren't the declared type. The
    // file is written to a temp file, checked, moved into place, and only then is UploadedAt set; a failure on the way
    // leaves no file and UploadedAt null.
    public async Task CompleteAsync(string token, Stream body, long? contentLength, CancellationToken cancellationToken)
    {
        var hash = SecretTokens.Hash(token);
        var image = await LiveUpload(hash).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (image == null)
        {
            throw new NotFoundException(UPLOAD_NOT_FOUND);
        }
        if (contentLength > ImageStore.MAX_BYTES)
        {
            throw new PayloadTooLargeException(ImageStore.TooLargeMessage);
        }

        var (temp, size) = await store.WriteTempAsync(body, cancellationToken);
        try
        {
            if (!ImageTypes.Matches(image.ContentType, await ImageStore.HeaderOfAsync(temp, cancellationToken)))
            {
                throw new BadRequestException($"The body is not a {ImageTypes.Label(image.ContentType)} image, the type upload_image declared");
            }
            await ConsumeAsync(image.Id, hash);
            await StoreAsync(image, temp, size);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    // The image's file when its upload has completed and the caller may view its deck; otherwise 404, the same for an
    // unknown image as for a forbidden one.
    public async Task<StoredImage> OpenAsync(string imageId)
    {
        var image = await db.DeckImages.AsNoTracking().SingleOrDefaultAsync(i => i.Id == imageId && i.UploadedAt != null);
        // A headless render page reads its own deck's images with its render token (L9), whoever the deck is shared with.
        if (image == null || (!renderSessions.AllowsImagesOf(image.DeckId) && !await access.CanViewAsync(image.DeckId, actor)))
        {
            throw new NotFoundException(IMAGE_NOT_FOUND);
        }
        return new StoredImage(store.PathFor(image.DeckId, image.Id), image.ContentType, $"\"{image.Id}.{image.UploadedAt!.Value.ToUnixTimeMilliseconds()}\"");
    }

    // The image whose upload this token still opens: unused, unexpired, and on a deck whose owner is approved now.
    private IQueryable<DeckImage> LiveUpload(string hash)
    {
        var now = clock.GetUtcNow();
        return db.DeckImages.Where(i => i.UploadTokenHash == hash && i.UploadedAt == null && i.UploadExpiresAt > now
            && db.Decks.Any(d => d.Id == i.DeckId && db.Users.Any(u => u.Id == d.OwnerId && u.Access == UserAccess.Approved)));
    }

    // Takes the token: only the first caller still holding a live token changes the row, and the checks run again in the
    // same statement, so an owner rejected while the body was arriving gets nothing stored.
    private async Task ConsumeAsync(string imageId, string hash)
    {
        var taken = await LiveUpload(hash).Where(i => i.Id == imageId)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.UploadTokenHash, (string?)null));
        if (taken == 0)
        {
            throw new NotFoundException(UPLOAD_NOT_FOUND);
        }
    }

    // Moves the checked file into place, then marks the upload complete. If marking it fails, the file goes too, so a
    // file never exists for an image whose UploadedAt is null.
    private async Task StoreAsync(DeckImage image, string temp, long size)
    {
        var stored = store.Commit(temp, image.DeckId, image.Id);
        try
        {
            await db.DeckImages.Where(i => i.Id == image.Id).ExecuteUpdateAsync(set => set
                .SetProperty(i => i.UploadedAt, clock.GetUtcNow())
                .SetProperty(i => i.SizeBytes, size)
                .SetProperty(i => i.StoragePath, ImageStore.RelativePathFor(image.DeckId, image.Id)));
        }
        catch
        {
            File.Delete(stored);
            throw;
        }
    }

    private static string CheckName(string name)
    {
        if (name.Any(char.IsControl))
        {
            throw new BadRequestException("name: must not contain control characters");
        }
        var label = name.Trim();
        if (label.Length == 0)
        {
            throw new BadRequestException("name: required");
        }
        if (label.Length > NAME_MAX_LENGTH)
        {
            throw new BadRequestException($"name: at most {NAME_MAX_LENGTH} characters");
        }
        return label;
    }
}
