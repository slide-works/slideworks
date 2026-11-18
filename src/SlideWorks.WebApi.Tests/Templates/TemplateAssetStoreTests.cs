using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// ITemplateAssets.StoreAsync, the way in for a caller that has already checked who owns the template (a bundle
// import), and where TemplateAssetStore puts a file. Art goes through a RefusingAssetCheck, so a test can refuse it.
[TestFixture]
public class TemplateAssetStoreTests : ApiTestFixture
{
    private const string MANIFEST = """{"name":"Northwind"}""";
    private const string ASSET = "0123456789abcdef0123456789abcdef";

    private readonly RefusingAssetCheck _art = new(TemplateAssetKind.Art);

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IAssetCheck>(_art);

    private TemplateAssetStore Store => Factory.Services.GetRequiredService<TemplateAssetStore>();

    [TearDown]
    public void AcceptAgain() => _art.Accept();

    [Test]
    public async Task Store_WhenTheCheckAccepts_ShouldStoreTheFileUnconfirmedUnderItsHash()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        var font = FontFixtures.Normal();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(font));

        var stored = await StoreAsync(template, TemplateAssetKind.Font, "fonts/inter.woff2", font);

        stored.AssetId.ShouldBe(sha256[..32]);
        stored.Kind.ShouldBe(TemplateAssetKind.Font);
        stored.FileName.ShouldBe("fonts/inter.woff2");
        stored.ContentType.ShouldBe("font/woff2");
        stored.Bytes.ShouldBe(font.Length);
        var asset = (await AssetsAsync(template)).ShouldHaveSingleItem();
        asset.Sha256.ShouldBe(sha256);
        asset.RightsConfirmed.ShouldBeFalse();
        asset.CreatedAt.ShouldBe(Clock.GetUtcNow());
        var path = Store.PathFor(template, stored.AssetId);
        path.ShouldEndWith(Path.Combine("templates", template, sha256[..32]));
        (await File.ReadAllBytesAsync(path)).ShouldBe(font);
    }

    [Test]
    public async Task Store_WhenTheCheckRefuses_ShouldStoreNothing()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);
        _art.Refuse(TemplateErrorCodes.NOT_AN_IMAGE);

        var refusal = await Should.ThrowAsync<TemplateRefusedException>(() => StoreAsync(template, TemplateAssetKind.Art, "art/title.png", Png()));

        refusal.Code.ShouldBe(TemplateErrorCodes.NOT_AN_IMAGE);
        refusal.Message.ShouldBe("Logos and art must be SVG, PNG or WebP files.");
        (await AssetsAsync(template)).ShouldBeEmpty();
        Directory.Exists(Path.GetDirectoryName(Store.PathFor(template, ASSET))).ShouldBeFalse();
    }

    [Test]
    public async Task Store_WhenTheFileIsOverTheLimit_ShouldRefuseBeforeTheCheck()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);

        var refusal = await Should.ThrowAsync<TemplateRefusedException>(() => StoreAsync(template, TemplateAssetKind.Art, "art/huge.png", new byte[(10 * 1024 * 1024) + 1]));

        refusal.Code.ShouldBe(TemplateErrorCodes.FILE_TOO_LARGE);
        refusal.Message.ShouldBe("This file is too large. The limit is 10 MB.");
        _art.LastLength().ShouldNotBe((10 * 1024 * 1024) + 1, "the check never sees a file over the limit");
        (await AssetsAsync(template)).ShouldBeEmpty();
    }

    [Test]
    public async Task Store_WhenTheFileIsExactlyAtTheLimit_ShouldStoreIt()
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);

        var stored = await StoreAsync(template, TemplateAssetKind.Art, "art/big.png", new byte[10 * 1024 * 1024]);

        stored.Bytes.ShouldBe(10 * 1024 * 1024);
        _art.LastLength().ShouldBe(10 * 1024 * 1024);
    }

    [Test]
    public async Task Store_WhenTheTemplateIsUnknown_ShouldBeNotFound()
    {
        var thrown = await Should.ThrowAsync<NotFoundException>(() => StoreAsync("../../etc", TemplateAssetKind.Art, "title.png", Png()));

        thrown.Message.ShouldBe("Template not found");
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("a\nb.png")]
    public async Task Store_WhenTheFileNameIsNotALabel_ShouldRefuse(string fileName)
    {
        var template = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, MANIFEST);

        await Should.ThrowAsync<BadRequestException>(() => StoreAsync(template, TemplateAssetKind.Art, fileName, Png()));

        (await AssetsAsync(template)).ShouldBeEmpty();
    }

    [TestCase("../tpl_abc", ASSET)]
    [TestCase("tpl_abc/..", ASSET)]
    [TestCase("images", ASSET)]
    [TestCase("", ASSET)]
    [TestCase("tpl_abc", "..")]
    [TestCase("tpl_abc", "0123456789ABCDEF0123456789ABCDEF")]
    [TestCase("tpl_abc", "0123456789abcdef0123456789abcde")]
    [TestCase("tpl_abc", "0123456789abcdef0123456789abcd/f")]
    public void PathFor_WhenAnIdIsNotOurs_ShouldThrow(string templateId, string assetId)
    {
        Should.Throw<InvalidOperationException>(() => Store.PathFor(templateId, assetId));
    }

    private async Task<StoredAsset> StoreAsync(string template, TemplateAssetKind kind, string fileName, byte[] bytes)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        using var content = new MemoryStream(bytes, writable: false);
        return await scope.ServiceProvider.GetRequiredService<ITemplateAssets>().StoreAsync(template, kind, fileName, content, CancellationToken.None);
    }

    private async Task<IReadOnlyList<TemplateAsset>> AssetsAsync(string template)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TemplateAssets.AsNoTracking().Where(a => a.TemplateId == template).ToListAsync();
    }

    // A real PNG of one pixel.
    private static byte[] Png()
        => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
}
