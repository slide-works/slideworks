namespace SlideWorks.WebApi.Features.Images;

// One entry of upload_images' result: what upload_image returns for one file, plus the name it was asked with.
public sealed record UploadImagesItem(string Image, string Name, string UploadUrl, string Method, int ExpiresInSeconds);
