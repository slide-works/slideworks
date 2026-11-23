namespace SlideWorks.WebApi.Features.Accounts.Profile;

// A person's uploaded photo: the file's stream, to be disposed by the caller, and its content type.
public sealed record ProfilePhoto(Stream Content, string ContentType);

public interface IProfilePhotos
{
    // The file and content type when photoId is that user's current PhotoId and the file exists, else null.
    Task<ProfilePhoto?> OpenAsync(Guid userId, string photoId);

    Task UploadAsync(Guid userId, Stream body, string contentType, CancellationToken cancellationToken);

    Task RemoveAsync(Guid userId);
}
