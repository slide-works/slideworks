using System.Security.Cryptography;
using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Images;
using SlideWorks.WebApi.Infrastructure;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

[RegisterPerRequest(For = typeof(IProfilePhotos))]
public class ProfilePhotos(AppDbContext db, AvatarFiles files) : IProfilePhotos
{
    private const int MAX_SIDE = 2048;
    private const int ID_LENGTH = 22;
    private const string ID_ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public async Task<ProfilePhoto?> OpenAsync(Guid userId, string photoId)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
        if (user?.PhotoId == null || user.PhotoContentType == null || !string.Equals(user.PhotoId, photoId, StringComparison.Ordinal))
        {
            return null;
        }
        var content = files.Open(userId, user.PhotoId);
        return content == null ? null : new ProfilePhoto(content, user.PhotoContentType);
    }

    // Checks come first and a refusal writes nothing: the declared type (not GIF), the 5 MB cap while streaming, the magic
    // bytes against the type, then the size from the header. Only then is the file moved into place and PhotoId switched.
    public async Task UploadAsync(Guid userId, Stream body, string contentType, CancellationToken cancellationToken)
    {
        var type = ImageTypes.Normalise(contentType.Split(';')[0]);
        if (type is null or ImageTypes.GIF)
        {
            throw new BadRequestException("A photo must be a PNG, JPEG or WebP image");
        }
        var temp = await files.WriteTempAsync(body, cancellationToken);
        var moved = false;
        string? placed = null;
        try
        {
            var bytes = await File.ReadAllBytesAsync(temp, cancellationToken);
            if (!ImageTypes.Matches(type, bytes))
            {
                throw new BadRequestException($"That file is not a {ImageTypes.Label(type)} image");
            }
            var size = PhotoHeader.SizeOf(type, bytes) ?? throw new BadRequestException("That looks like a damaged image");
            if (size.Width > MAX_SIDE || size.Height > MAX_SIDE)
            {
                throw new BadRequestException($"A photo may be at most {MAX_SIDE} pixels on a side");
            }
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken)
                ?? throw new NotFoundException("The signed-in person no longer exists.");
            var photoId = RandomNumberGenerator.GetString(ID_ALPHABET, ID_LENGTH);
            placed = files.Commit(temp, userId, photoId);
            moved = true;
            user.PhotoId = photoId;
            user.PhotoContentType = type;
            await db.SaveChangesAsync(cancellationToken);
            placed = null;
        }
        finally
        {
            if (!moved)
            {
                File.Delete(temp);
            }
            if (placed != null)
            {
                File.Delete(placed);
            }
        }
    }

    // Clears the reference and keeps the file (D4).
    public async Task RemoveAsync(Guid userId)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId) ?? throw new NotFoundException("The signed-in person no longer exists.");
        user.PhotoId = null;
        user.PhotoContentType = null;
        await db.SaveChangesAsync();
    }
}
