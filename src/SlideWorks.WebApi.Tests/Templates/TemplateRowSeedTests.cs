using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Decks;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

[TestFixture]
public class TemplateRowSeedTests : ApiTestFixture
{
    private const string MANIFEST = """{"name":"Northwind"}""";

    [Test]
    public async Task Seed_WhenAPublishedVersionIsWritten_ShouldReadBack()
    {
        var id = await TemplateRowSeed.PublishedAsync(Factory, TestUser.Sam, MANIFEST, version: 2, access: DeckSharing.Named);
        await TemplateRowSeed.ViewerAsync(Factory, id, TestUser.Alex.Email);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = await db.Templates.AsNoTracking().SingleAsync(t => t.Id == id);
        var owner = await db.Users.AsNoTracking().SingleAsync(u => u.Id == template.OwnerId);
        var version = await db.TemplateVersions.AsNoTracking().SingleAsync(v => v.TemplateId == id);
        var viewer = await db.TemplateViewers.AsNoTracking().SingleAsync(v => v.TemplateId == id);

        id.ShouldMatch("^tpl_[0-9A-Za-z]{12}$");
        owner.Email.ShouldBe(TestUser.Sam.Email);
        template.Access.ShouldBe(DeckSharing.Named);
        template.Revision.ShouldBe(1);
        template.CreatedAt.ShouldBe(Clock.GetUtcNow());
        template.UpdatedAt.ShouldBe(Clock.GetUtcNow());
        template.ArchivedAt.ShouldBeNull();
        version.Version.ShouldBe(2);
        version.ManifestJson.ShouldBe(MANIFEST);
        version.FitWidthsJson.ShouldBeNull();
        version.PublishedAt.ShouldBe(Clock.GetUtcNow());
        viewer.Email.ShouldBe(TestUser.Alex.Email);
    }

    [Test]
    public async Task Seed_WhenADraftIsWritten_ShouldReadBack()
    {
        var id = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var template = await db.Templates.AsNoTracking().SingleAsync(t => t.Id == id);
        var version = await db.TemplateVersions.AsNoTracking().SingleAsync(v => v.TemplateId == id);

        template.Access.ShouldBe(DeckSharing.Private);
        version.Version.ShouldBe(0);
        version.ManifestJson.ShouldBe(MANIFEST);
        version.PublishedAt.ShouldBeNull();
    }

    [Test]
    public async Task Seed_WhenAPendingUploadIsWritten_ShouldReadBack()
    {
        var id = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        var expires = Clock.GetUtcNow().AddMinutes(15);
        await using (var write = Factory.Services.CreateAsyncScope())
        {
            var writer = write.ServiceProvider.GetRequiredService<AppDbContext>();
            writer.TemplateUploads.Add(new TemplateUpload
            {
                TokenHash = "hash-of-a-token",
                TemplateId = id,
                Kind = TemplateAssetKind.Logo,
                FileName = "northwind.svg",
                RightsConfirmed = true,
                ExpiresAt = expires,
            });
            await writer.SaveChangesAsync();
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var upload = await db.TemplateUploads.AsNoTracking().SingleAsync(u => u.TokenHash == "hash-of-a-token");
        var storedKind = await db.Database
            .SqlQuery<string>($"SELECT \"Kind\" AS Value FROM \"TemplateUploads\" WHERE \"TokenHash\" = 'hash-of-a-token'")
            .SingleAsync();

        upload.TemplateId.ShouldBe(id);
        upload.Kind.ShouldBe(TemplateAssetKind.Logo);
        upload.FileName.ShouldBe("northwind.svg");
        upload.RightsConfirmed.ShouldBeTrue();
        upload.ExpiresAt.ShouldBe(expires);
        upload.UsedAt.ShouldBeNull();
        storedKind.ShouldBe("logo");
    }

    [Test]
    public async Task Seed_WhenAnAssetIsWritten_ShouldReadBack()
    {
        var id = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);

        var assetId = await TemplateRowSeed.AssetAsync(Factory, id, TemplateAssetKind.Font, "Brand-Sans.woff2", metaJson: """{"family":"Brand Sans"}""", rightsConfirmed: true);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = await db.TemplateAssets.AsNoTracking().SingleAsync(a => a.TemplateId == id);
        var storedKind = await db.Database
            .SqlQuery<string>($"SELECT \"Kind\" AS Value FROM \"TemplateAssets\" WHERE \"TemplateId\" = {id}")
            .SingleAsync();

        assetId.ShouldMatch("^[0-9a-f]{32}$");
        asset.Id.ShouldBe(assetId);
        asset.Sha256.ShouldStartWith(assetId, Case.Sensitive);
        asset.Sha256.Length.ShouldBe(64);
        asset.Kind.ShouldBe(TemplateAssetKind.Font);
        asset.FileName.ShouldBe("Brand-Sans.woff2");
        asset.ContentType.ShouldBe("font/woff2");
        asset.MetaJson.ShouldBe("""{"family":"Brand Sans"}""");
        asset.RightsConfirmed.ShouldBeTrue();
        asset.CreatedAt.ShouldBe(Clock.GetUtcNow());
        storedKind.ShouldBe("font");
    }
}
