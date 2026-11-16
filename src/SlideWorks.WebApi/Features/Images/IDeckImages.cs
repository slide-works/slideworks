namespace SlideWorks.WebApi.Features.Images;

// The images a deck's slides may point at (K11).
public interface IDeckImages
{
    // True only when the image belongs to the deck and its upload has completed (UploadedAt is set).
    Task<bool> ExistsAsync(string deckId, string imageId);
}
