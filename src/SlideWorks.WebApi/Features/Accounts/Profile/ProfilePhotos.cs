using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Accounts.Profile;

[RegisterPerRequest(For = typeof(IProfilePhotos))]
public class ProfilePhotos(AppDbContext db, AvatarFiles files) : IProfilePhotos
{
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

    // TODO: replace with the real upload: check type, bytes and size, write through AvatarFiles, set PhotoId and PhotoContentType.
    public Task UploadAsync(Guid userId, Stream body, string contentType, CancellationToken cancellationToken)
        => throw new NotImplementedException("Photo upload is not built yet.");

    // TODO: replace with the real removal: clear PhotoId and PhotoContentType and keep the file.
    public Task RemoveAsync(Guid userId) => throw new NotImplementedException("Photo removal is not built yet.");
}
