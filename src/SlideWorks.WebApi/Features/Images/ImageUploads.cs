using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Activity;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Realtime;

namespace SlideWorks.WebApi.Features.Images;

// Images on a deck (K11): upload_image makes an image and a one-time upload URL, PUT /api/uploads/{token} brings its
// bytes, and GET /api/images/{id} serves them to whoever may view the deck.
//
// The upload token is 256 random bits, stored only as its SHA-256 hash, and valid for 10 minutes. It is consumed by one
// conditional update, so of two PUTs racing with it exactly one stores the image and the other gets 404. A body that is
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
    // says "uploaded story-map.png"); it never reaches a file path.
    public async Task<UploadImageResult> CreateAsync(string deckId, string name, string contentType)
    {
        if (!await access.CanEditAsync(deckId, actor))
        {
            throw new NotFoundException(DeckReader.NOT_FOUND);
        }
        var label = CheckName(name);
        var type = ImageTypes.Normalise(contentType) ?? throw new BadRequestException($"contentType: must be {ImageTypes.ACCEPTED_LIST}");

        var token = OAuthSecrets.New();
        var image = new DeckImage
        {
            Id = ID_PREFIX + RandomNumberGenerator.GetString(ALPHABET, ID_RANDOM_LENGTH),
            DeckId = deckId,
            Name = label,
            ContentType = type,
            UploadTokenHash = OAuthSecrets.Hash(token),
            UploadExpiresAt = clock.GetUtcNow() + UPLOAD_LIFETIME,
        };
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.DeckImages.Add(image);
            await db.SaveChangesAsync();
            await activity.RecordAsync(deckId, ActivityEntry.By(actor, TOOL, $"uploaded {label}"));
            await transaction.CommitAsync();
        }
        await events.PublishAsync(new DeckChanged(deckId));
        return new UploadImageResult(image.Id, $"{urls.Base}/api/uploads/{token}", "PUT", (int)UPLOAD_LIFETIME.TotalSeconds);
    }

    // 404 for a token that is unknown, used or expired; 413 over 10 MB; 400 when the bytes aren't the declared type. The
    // file is written to a temp file, checked, moved into place, and only then is UploadedAt set; a failure on the way
    // leaves no file and UploadedAt null.
    public async Task CompleteAsync(string token, Stream body, long? contentLength, CancellationToken cancellationToken)
    {
        var hash = OAuthSecrets.Hash(token);
        var image = await db.DeckImages.AsNoTracking().SingleOrDefaultAsync(i => i.UploadTokenHash == hash, cancellationToken);
        if (image == null || image.UploadedAt != null || image.UploadExpiresAt <= clock.GetUtcNow())
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
        if (image == null || !await access.CanViewAsync(image.DeckId, actor))
        {
            throw new NotFoundException(IMAGE_NOT_FOUND);
        }
        return new StoredImage(store.PathFor(image.DeckId, image.Id), image.ContentType, $"\"{image.Id}.{image.UploadedAt!.Value.ToUnixTimeMilliseconds()}\"");
    }

    // Takes the token: only the first caller still holding a live, unused token changes the row.
    private async Task ConsumeAsync(string imageId, string hash)
    {
        var now = clock.GetUtcNow();
        var taken = await db.DeckImages
            .Where(i => i.Id == imageId && i.UploadTokenHash == hash && i.UploadedAt == null && i.UploadExpiresAt > now)
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
