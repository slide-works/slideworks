namespace SlideWorks.WebApi.Features.Images;

public class DeckImage
{
    // "img_" plus 10 base62 characters.
    public required string Id { get; set; }

    public required string DeckId { get; set; }

    public required string Name { get; set; }

    public required string ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public string? StoragePath { get; set; }

    public string? UploadTokenHash { get; set; }

    public DateTimeOffset? UploadExpiresAt { get; set; }

    // Null until the bytes have arrived.
    public DateTimeOffset? UploadedAt { get; set; }
}
