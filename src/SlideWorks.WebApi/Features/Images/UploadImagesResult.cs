namespace SlideWorks.WebApi.Features.Images;

// upload_images: { images: [{ image, name, uploadUrl, method, expiresInSeconds }] }. Each uploadUrl still takes one PUT
// of the raw bytes. name is the file name the call gave, so the caller can match an id to a file.
public sealed record UploadImagesResult(IReadOnlyList<UploadImageFile> Images);

public sealed record UploadImageFile(string Image, string Name, string UploadUrl, string Method, int ExpiresInSeconds);
