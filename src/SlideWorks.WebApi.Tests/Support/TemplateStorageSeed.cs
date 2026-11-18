using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Tests.Support;

// Fills a template's storage with rows only, for the tests of what one template may store: reaching the limits through
// the upload route would take 256 uploads or 240 MB. No file is written; each row's hash is the hash of its label.
public static class TemplateStorageSeed
{
    // Adds that many art rows of that size each to the template.
    public static async Task FillAsync(WebApplicationFactory<ApiMarker> factory, string templateId, int rows, long bytesEach)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        // Numbered on from the rows the template has, so a second call adds new rows.
        var first = await db.TemplateAssets.CountAsync(a => a.TemplateId == templateId);
        for (var i = 0; i < rows; i++)
        {
            var fileName = $"stored-{first + i}.png";
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{templateId}/{fileName}")));
            db.TemplateAssets.Add(new TemplateAsset
            {
                Id = sha256[..TemplateAsset.ID_LENGTH],
                TemplateId = templateId,
                Kind = TemplateAssetKind.Art,
                FileName = fileName,
                ContentType = "image/png",
                Sha256 = sha256,
                Bytes = bytesEach,
                CreatedAt = now,
            });
        }
        await db.SaveChangesAsync();
    }
}
