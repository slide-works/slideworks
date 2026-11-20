using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates.Data;

namespace SlideWorks.WebApi.Tests.Support;

// Writes template rows straight to the database, for tests only: a template with one published version or with a draft,
// a file's row and a named viewer. It lets a task test against templates without another task's code. It is the only
// place a test writes template rows, and the one exception to seeding through the API. Stamps come from the app's clock.
// The owner is signed in through TestPeople, as every test person is.
public static class TemplateRowSeed
{
    private const string NAME = "Seeded template";

    // A template whose only version is published under that number. Returns the template's "tpl_" id.
    public static Task<string> PublishedAsync(
        WebApplicationFactory<ApiMarker> factory, TestUser owner, string manifestJson, int version = 1, DeckSharing access = DeckSharing.Private)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Published versions count from 1; use DraftAsync for the draft.");
        }
        return TemplateAsync(factory, owner, manifestJson, version, access);
    }

    // A private template that has only its draft (version 0). Returns the template's "tpl_" id.
    public static Task<string> DraftAsync(WebApplicationFactory<ApiMarker> factory, TestUser owner, string manifestJson)
        => TemplateAsync(factory, owner, manifestJson, TemplateVersion.DRAFT, DeckSharing.Private);

    // A file's row only: no file is written to disk. Its hash is the hash of its name, so the same name gives the same id.
    // Returns the asset's id.
    public static async Task<string> AssetAsync(
        WebApplicationFactory<ApiMarker> factory, string templateId, TemplateAssetKind kind, string fileName, string? metaJson = null, bool rightsConfirmed = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fileName)));
        var asset = new TemplateAsset
        {
            Id = sha256[..TemplateAsset.ID_LENGTH],
            TemplateId = templateId,
            Kind = kind,
            FileName = fileName,
            ContentType = ContentTypeOf(kind),
            Sha256 = sha256,
            Bytes = Encoding.UTF8.GetByteCount(fileName),
            MetaJson = metaJson,
            RightsConfirmed = rightsConfirmed,
            CreatedAt = Now(scope),
        };
        db.TemplateAssets.Add(asset);
        await db.SaveChangesAsync();
        return asset.Id;
    }

    public static async Task ViewerAsync(WebApplicationFactory<ApiMarker> factory, string templateId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TemplateViewers.Add(new TemplateViewer { TemplateId = templateId, Email = email });
        await db.SaveChangesAsync();
    }

    private static async Task<string> TemplateAsync(
        WebApplicationFactory<ApiMarker> factory, TestUser owner, string manifestJson, int version, DeckSharing access)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await TestPeople.SignInAsync(scope.ServiceProvider, owner);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = Now(scope);
        var template = new TemplateRecord
        {
            Id = TemplateRecord.ID_PREFIX + DeckIds.New(),
            OwnerId = user.Id,
            Name = NAME,
            Access = access,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Templates.Add(template);
        db.TemplateVersions.Add(new TemplateVersion
        {
            TemplateId = template.Id,
            Version = version,
            ManifestJson = manifestJson,
            PublishedAt = version == TemplateVersion.DRAFT ? null : now,
        });
        await db.SaveChangesAsync();
        return template.Id;
    }

    private static string ContentTypeOf(TemplateAssetKind kind) => kind switch
    {
        TemplateAssetKind.Font => "font/woff2",
        TemplateAssetKind.Logo => "image/svg+xml",
        TemplateAssetKind.Art => "image/png",
        TemplateAssetKind.Layout => "text/html",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static DateTimeOffset Now(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
}
