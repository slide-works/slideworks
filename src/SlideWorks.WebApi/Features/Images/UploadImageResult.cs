namespace SlideWorks.WebApi.Features.Images;

// upload_image: { image, uploadUrl, method: "PUT", expiresInSeconds: 600 } (K6). image is the id to put in an image slot
// once the bytes are PUT to uploadUrl, an absolute URL that works once and expires.
public sealed record UploadImageResult(string Image, string UploadUrl, string Method, int ExpiresInSeconds);
