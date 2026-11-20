namespace SlideWorks.WebApi.Features.Images;

// upload_images: { images: [{ image, name, uploadUrl, method: "PUT", expiresInSeconds: 600 }] }, in the order the files
// were named. Each image is the id to put in an image slot once its bytes are PUT to its uploadUrl.
public sealed record UploadImagesResult(IReadOnlyList<UploadImagesItem> Images);
