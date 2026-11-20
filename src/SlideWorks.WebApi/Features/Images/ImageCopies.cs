using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Images;

// Copies one deck's completed logo onto another deck: a new image id and a new file, never the source id. The row joins
// the caller's current transaction. The file is the caller's to delete when that transaction does not commit.
[RegisterPerRequest]
public class ImageCopies(AppDbContext db, ImageStore store)
{
    private const string ID_PREFIX = "img_";
    private const int ID_LENGTH = 10;
    private const string ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public async Task<(string Id, string Path)> CopyOntoAsync(string sourceDeckId, string sourceImageId, string targetDeckId, DateTimeOffset uploadedAt)
    {
        var source = await db.DeckImages.AsNoTracking()
            .SingleOrDefaultAsync(image => image.Id == sourceImageId && image.DeckId == sourceDeckId && image.UploadedAt != null)
            ?? throw new InvalidOperationException($"Deck {sourceDeckId} has no completed image {sourceImageId}.");

        var id = ID_PREFIX + RandomNumberGenerator.GetString(ALPHABET, ID_LENGTH);
        var path = store.Copy(sourceDeckId, sourceImageId, targetDeckId, id);
        db.DeckImages.Add(new DeckImage
        {
            Id = id,
            DeckId = targetDeckId,
            Name = source.Name,
            ContentType = source.ContentType,
            SizeBytes = source.SizeBytes,
            StoragePath = ImageStore.RelativePathFor(targetDeckId, id),
            UploadedAt = uploadedAt,
        });
        return (id, path);
    }
}
