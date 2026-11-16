using Agoda.IoC.Core;
using Microsoft.EntityFrameworkCore;
using SlideWorks.WebApi.Data;

namespace SlideWorks.WebApi.Features.Images;

[RegisterPerRequest(For = typeof(IDeckImages))]
public class EfDeckImages(AppDbContext db) : IDeckImages
{
    public Task<bool> ExistsAsync(string deckId, string imageId)
        => db.DeckImages.AnyAsync(i => i.Id == imageId && i.DeckId == deckId && i.UploadedAt != null);
}
