namespace SlideWorks.WebApi.Features.Images;

// The body of POST /api/decks/{id}/images (L6 image-upload.json): the same fields upload_image takes. ImageUploads
// checks both, so a missing one is refused with the same message the tool gives.
public sealed record ImageUploadRequest(string? Name, string? ContentType);
