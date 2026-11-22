using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core.Look.Manifest;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Features.Templates;
using SlideWorks.WebApi.Features.Templates.Assets;
using SlideWorks.WebApi.Features.Templates.Bundles;
using SlideWorks.WebApi.Features.Templates.Data;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Templates;

// Bundle import and export (D4) through the routes, on the real drafts, asset store and file checks. Every zip is built
// here with ZipArchive from the contract's sample manifest: the fonts are FontFixtures.Normal() (the sample's four font
// paths are one stored file, because storage is content-addressed), the logos and art are one-rectangle SVGs, and the
// layout goes through a RefusingAssetCheck that accepts, as the app's own layout check refuses every layout for now.
// The fixture's app is shared, so each test compares the database and the data folder before and after, never to zero.
[TestFixture]
public class TemplateBundleTests : ApiTestFixture
{
    private const string ZIP = "application/zip";
    private const string SVG_NAMESPACE = "http://www.w3.org/2000/svg";
    private const string LAYOUT_HTML = "<div class=\"ladder\"><slot name=\"title\"></slot></div>";
    private const string LAYOUT_CSS = ".ladder { display: grid; }";
    private const string LAYOUT_SLOTS = """{"title":{"type":"text"}}""";

    private readonly RefusingAssetCheck _layout = new(TemplateAssetKind.Layout);

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IAssetCheck>(_layout);

    [Test]
    public async Task Import_WhenTheBundleIsTheSample_ShouldMakeADraft()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle());
        var body = await JsonAsync(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, body.ToString());
        var id = body.GetProperty("id").GetString()!;
        var after = await SnapshotAsync();
        after.Templates.ShouldBe(before.Templates + 1);
        after.Assets.ShouldBe(before.Assets + 5);
        var assets = await AssetsAsync(id);
        assets.Select(a => a.Kind).Order().ShouldBe([TemplateAssetKind.Font, TemplateAssetKind.Logo, TemplateAssetKind.Logo, TemplateAssetKind.Art, TemplateAssetKind.Layout]);
        assets.ShouldAllBe(a => !a.RightsConfirmed);
        var stored = await DraftManifestAsync(id);
        stored.Label.ShouldBe("Northwind kickoff");
        stored.Fonts.Select(f => f.File).Distinct().ShouldHaveSingleItem().ShouldBe(assets.Single(a => a.Kind == TemplateAssetKind.Font).Id);
        stored.Logos.Light.ShouldBe(assets.Single(a => a.FileName == "logo-light.svg").Id);
        stored.TitleArt.File.ShouldBe(assets.Single(a => a.Kind == TemplateAssetKind.Art).Id);
        stored.CustomLayouts.ShouldHaveSingleItem().File.ShouldBe(assets.Single(a => a.Kind == TemplateAssetKind.Layout).Id);
        File.Exists(Path.Combine(DataPath(), "templates", id, assets.Single(a => a.Kind == TemplateAssetKind.Font).Id)).ShouldBeTrue();
    }

    [TestCase("../x")]
    [TestCase("a/../../x")]
    [TestCase("/etc/x")]
    [TestCase("fonts\\..\\x")]
    [TestCase("C:/x")]
    [TestCase("a//b")]
    [TestCase("a\0b")]
    public async Task Import_WhenAPathEscapesTheBundle_ShouldRefuse(string name)
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(zip => AddBytes(zip, name, [1, 2, 3])));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [TestCase("../x")]
    [TestCase("/etc/x")]
    [TestCase("fonts\\a.woff2")]
    [TestCase("C:/x")]
    [TestCase("fonts//a.woff2")]
    public async Task Import_WhenTheManifestNamesAPathOutsideTheBundle_ShouldChangeNothing(string path)
    {
        var before = await SnapshotAsync();
        var manifest = SampleManifest().Replace("fonts/northwind-sans-700.woff2", path, StringComparison.Ordinal);

        var response = await ImportAsync(SampleBundle(manifest: manifest));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenTheBundleExpandsPastTheLimit_ShouldRefuse()
    {
        var before = await SnapshotAsync();
        var zeros = new byte[TemplateLimits.MAX_BUNDLE_BYTES + 1024 * 1024];

        var response = await ImportAsync(SampleBundle(zip => AddBytes(zip, "fonts/zeros.bin", zeros)));

        response.Content.Headers.ContentLength.GetValueOrDefault().ShouldBeLessThan(TemplateLimits.MAX_BUNDLE_BYTES);
        await ShouldRefuseAsync(response, "bundle_too_large");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenAFileUnpacksToFarMoreThanItPacks_ShouldRefuse()
    {
        var before = await SnapshotAsync();
        var manifest = SampleManifest().Replace("art.svg", "art.png", StringComparison.Ordinal);
        var bundle = SampleBundle(zip => AddBytes(zip, "art.png", new byte[5 * 1024 * 1024]), manifest, without: ["art.svg"]);

        var response = await ImportAsync(bundle);

        await ShouldRefuseAsync(response, "bundle_too_large");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenAFontIsOverItsSize_ShouldRefuse()
    {
        var before = await SnapshotAsync();
        var big = new byte[TemplateLimits.MAX_FONT_BYTES + 1];
        new Random(7).NextBytes(big);

        var response = await ImportAsync(SampleBundle(replace: new() { ["fonts/northwind-mono-400.woff2"] = big }));

        await ShouldRefuseAsync(response, "bundle_too_large");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [TestCase(1u)]
    [TestCase(100_000_000u)]
    public async Task Import_WhenAnEntryLiesAboutItsSize_ShouldRefuse(uint declared)
    {
        var before = await SnapshotAsync();
        var bundle = DeclareSize(SampleBundle(), "fonts/northwind-text-400.woff2", declared);

        var response = await ImportAsync(bundle);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenTheManifestIsMissing_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(without: ["template.json"]));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenTheManifestIsNotAValidManifest_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(manifest: """{"name":"x"}"""));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenTheManifestIsBasedOnNoBuiltIn_ShouldRefuse()
    {
        var before = await SnapshotAsync();
        var manifest = SampleManifest().Replace("\"basedOn\": \"workshop\"", "\"basedOn\": \"nothing-like-it\"", StringComparison.Ordinal);

        var response = await ImportAsync(SampleBundle(manifest: manifest));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenTheBodyIsNotAZip_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(Encoding.UTF8.GetBytes("this is not a zip file at all"));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenAFileFailsItsCheck_ShouldChangeNothing()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(replace: new() { ["fonts/northwind-mono-400.woff2"] = FontFixtures.NotAFont() }));

        var body = await ShouldRefuseAsync(response, "font_not_woff2");
        body.GetProperty("path").GetString().ShouldBe("fonts/northwind-mono-400.woff2");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenALogoIsAScript_ShouldPassOnTheChecksFindingsAndChangeNothing()
    {
        var before = await SnapshotAsync();
        var script = Encoding.UTF8.GetBytes($"""<svg xmlns="{SVG_NAMESPACE}"><script>alert(1)</script></svg>""");

        var response = await ImportAsync(SampleBundle(replace: new() { ["logo-dark.svg"] = script }));

        var body = await ShouldRefuseAsync(response, "svg_script");
        body.GetProperty("errors").ValueKind.ShouldBe(JsonValueKind.Array);
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenALayoutIsRefusedByItsCheck_ShouldChangeNothing()
    {
        var before = await SnapshotAsync();
        _layout.Refuse(TemplateErrorCodes.CUSTOM_LAYOUTS_UNAVAILABLE);

        try
        {
            var response = await ImportAsync(SampleBundle());

            await ShouldRefuseAsync(response, "custom_layouts_unavailable");
            (await SnapshotAsync()).ShouldBe(before);
        }
        finally
        {
            _layout.Accept();
        }
    }

    [Test]
    public async Task Import_WhenIHaveFiftyTemplates_ShouldChangeNothing()
    {
        for (var i = 0; i < TemplateLimits.MAX_TEMPLATES_PER_PERSON; i++)
        {
            await TemplateRowSeed.DraftAsync(Factory, TestUser.Alex, "{}");
        }
        var before = await SnapshotAsync();
        using var alex = ClientFor(TestUser.Alex);

        var response = await ImportAsync(SampleBundle(), alex);

        await ShouldRefuseAsync(response, "too_many_templates");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenOverTheEntryLimit_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(zip =>
        {
            for (var i = 0; i < TemplateLimits.MAX_BUNDLE_ENTRIES; i++)
            {
                AddBytes(zip, $"extra/{i}.txt", [1]);
            }
        }));

        await ShouldRefuseAsync(response, "bundle_too_large");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenTheSameNameIsUsedTwice_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(zip => AddBytes(zip, "logo-light.svg", Svg(99))));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenTheSameNameDiffersOnlyInCase_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(zip => AddBytes(zip, "LOGO-LIGHT.svg", Svg(99))));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenAnEntryIsASymlink_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(zip =>
        {
            var link = AddBytes(zip, "link", Encoding.UTF8.GetBytes("/etc/passwd"));
            link.ExternalAttributes = (0xA000 | 0x1FF) << 16;
        }));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenASymlinkIsNamedByTheManifest_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(linked: "art.svg"));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenAFileIsNotNamedByTheManifest_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(zip => AddBytes(zip, "notes.txt", [1, 2, 3])));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenTheManifestNamesAFileTheBundleLacks_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(without: ["art.svg"]));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenALayoutLacksOneOfItsThreeFiles_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(without: ["layouts/stage-ladder.css"]));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenALayoutsSlotsAreNotAnObject_ShouldRefuse()
    {
        var before = await SnapshotAsync();

        var response = await ImportAsync(SampleBundle(replace: new() { ["layouts/stage-ladder.json"] = Encoding.UTF8.GetBytes("[1]") }));

        await ShouldRefuseAsync(response, "bundle_invalid");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenAPartOfALayoutIsTooLarge_ShouldRefuse()
    {
        var before = await SnapshotAsync();
        var css = new byte[TemplateLimits.MAX_LAYOUT_PART_BYTES + 1];
        new Random(3).NextBytes(css);

        var response = await ImportAsync(SampleBundle(replace: new() { ["layouts/stage-ladder.css"] = css }));

        await ShouldRefuseAsync(response, "bundle_too_large");
        (await SnapshotAsync()).ShouldBe(before);
    }

    [Test]
    public async Task Import_WhenALayoutPassesItsCheck_ShouldStoreOneDocument()
    {
        var response = await ImportAsync(SampleBundle());
        var id = (await JsonAsync(response)).GetProperty("id").GetString()!;

        var layout = (await AssetsAsync(id)).Single(a => a.Kind == TemplateAssetKind.Layout);
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(DataPath(), "templates", id, layout.Id)));

        document.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(["markup", "css", "slots"]);
        document.RootElement.GetProperty("markup").GetString().ShouldBe(LAYOUT_HTML);
        document.RootElement.GetProperty("css").GetString().ShouldBe(LAYOUT_CSS);
        document.RootElement.GetProperty("slots").GetRawText().ShouldBe(LAYOUT_SLOTS);
    }

    [Test]
    public async Task Export_WhenATemplateHasALayout_ShouldWriteItsThreeFiles()
    {
        var id = await ImportedIdAsync();

        var response = await Client.GetAsync($"/api/templates/{id}/bundle");
        var entries = ReadZip(await response.Content.ReadAsByteArrayAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(ZIP);
        response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("northwind-kickoff.zip");
        entries["layouts/stage-ladder.html"].ShouldBe(Encoding.UTF8.GetBytes(LAYOUT_HTML));
        entries["layouts/stage-ladder.css"].ShouldBe(Encoding.UTF8.GetBytes(LAYOUT_CSS));
        entries["layouts/stage-ladder.json"].ShouldBe(Encoding.UTF8.GetBytes(LAYOUT_SLOTS));
        entries.Keys.Order().ShouldBe(
            [
                "art.svg", $"fonts/{(await AssetsAsync(id)).Single(a => a.Kind == TemplateAssetKind.Font).Id}.woff2", "layouts/stage-ladder.css",
                "layouts/stage-ladder.html", "layouts/stage-ladder.json", "logo-dark.svg", "logo-light.svg", "template.json",
            ],
            ignoreOrder: true);
    }

    [Test]
    public async Task Export_WhenImportedAgain_ShouldGiveTheSameManifest()
    {
        var first = ReadZip(await (await Client.GetAsync($"/api/templates/{await ImportedIdAsync()}/bundle")).Content.ReadAsByteArrayAsync());
        var again = await ImportAsync(ZipOf(first));
        var againId = (await JsonAsync(again)).GetProperty("id").GetString()!;

        var second = ReadZip(await (await Client.GetAsync($"/api/templates/{againId}/bundle")).Content.ReadAsByteArrayAsync());

        again.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Keys.Order().ShouldBe(first.Keys.Order());
        Encoding.UTF8.GetString(second["template.json"]).ShouldBe(Encoding.UTF8.GetString(first["template.json"]));
        second.ShouldAllBe(entry => entry.Value.SequenceEqual(first[entry.Key]));
    }

    [Test]
    public async Task Export_WhenTheTemplateIsOnlyADraft_ShouldGiveTheDraft()
    {
        var id = await TemplateRowSeed.DraftAsync(Factory, TestUser.Sam, SampleManifestWithoutFiles());

        var response = await Client.GetAsync($"/api/templates/{id}/bundle");
        var entries = ReadZip(await response.Content.ReadAsByteArrayAsync());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        entries.Keys.ShouldBe(["template.json"]);
    }

    [Test]
    public async Task Export_WhenNotTheOwner_ShouldBeNotFound()
    {
        var id = await ImportedIdAsync();
        using var mira = ClientFor(TestUser.Mira);

        var response = await mira.GetAsync($"/api/templates/{id}/bundle");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Export_WhenTheTemplateDoesNotExist_ShouldBeNotFound()
    {
        var response = await Client.GetAsync("/api/templates/tpl_doesnotexist/bundle");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestCase("../../x", "x.zip")]
    [TestCase("a/b", "a-b.zip")]
    [TestCase("a\\b", "a-b.zip")]
    [TestCase("", "template.zip")]
    [TestCase("....", "template.zip")]
    [TestCase("northwind-kickoff", "northwind-kickoff.zip")]
    [TestCase("a\r\nb\"c", "a--b-c.zip")]
    public void Export_WhenTheShortNameIsHostile_ShouldSendASafeFileName(string shortName, string expected)
    {
        BundleNames.DownloadName(shortName).ShouldBe(expected);
        BundleNames.DownloadName(new string('a', 300)).ShouldBe(new string('a', 60) + ".zip");
    }

    private async Task<string> ImportedIdAsync()
    {
        var response = await ImportAsync(SampleBundle());
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await JsonAsync(response)).GetProperty("id").GetString()!;
    }

    private async Task<HttpResponseMessage> ImportAsync(byte[] zip, HttpClient? client = null)
    {
        using var content = new ByteArrayContent(zip);
        content.Headers.ContentType = new MediaTypeHeaderValue(ZIP);
        return await (client ?? Client).PostAsync("/api/templates/import", content);
    }

    private static async Task<JsonElement> ShouldRefuseAsync(HttpResponseMessage response, string code)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, text);
        using var document = JsonDocument.Parse(text);
        document.RootElement.GetProperty("error").GetString().ShouldBe(code, text);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private string DataPath() => Factory.Services.GetRequiredService<IConfiguration>()["SLIDEWORKS_DATA_PATH"]!;

    private sealed record Snapshot(int Templates, int Assets, int Versions, int Files);

    // What an import may change: template rows, version rows, file rows and the files on disk (the temp folder included).
    private async Task<Snapshot> SnapshotAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var folder = Path.Combine(DataPath(), "templates");
        var files = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count() : 0;
        return new Snapshot(await db.Templates.CountAsync(), await db.TemplateAssets.CountAsync(), await db.TemplateVersions.CountAsync(), files);
    }

    private async Task<List<TemplateAsset>> AssetsAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TemplateAssets.AsNoTracking().Where(a => a.TemplateId == id).ToListAsync();
    }

    private async Task<TemplateManifest> DraftManifestAsync(string id)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var draft = await db.TemplateVersions.AsNoTracking().SingleAsync(v => v.TemplateId == id && v.Version == TemplateVersion.DRAFT);
        return ManifestJson.Parse(draft.ManifestJson).Manifest!;
    }

    private static string SampleManifest()
        => File.ReadAllText(Path.Combine(JsonShape.ScreensDirectory(), "..", "template-manifest.sample.json"));

    // The sample with no file fields: no fonts, logos, art or layouts.
    private static string SampleManifestWithoutFiles()
    {
        var manifest = ManifestJson.Parse(SampleManifest()).Manifest!;
        return ManifestJson.Write(manifest with
        {
            Fonts = [],
            Logos = manifest.Logos with { Light = null, Dark = null },
            TitleArt = manifest.TitleArt with { Kind = "none", File = null },
            CustomLayouts = [],
        });
    }

    // A bundle of the sample: template.json (or manifest), then every file it names. replace swaps a file's bytes,
    // without drops a file (or template.json), linked makes one entry a symlink, and extra adds entries after them.
    private static byte[] SampleBundle(
        Action<ZipArchive>? extra = null, string? manifest = null, IReadOnlyCollection<string>? without = null, Dictionary<string, byte[]>? replace = null, string? linked = null)
    {
        var files = new Dictionary<string, byte[]>
        {
            ["template.json"] = Encoding.UTF8.GetBytes(manifest ?? SampleManifest()),
            ["fonts/northwind-sans-700.woff2"] = FontFixtures.Normal(),
            ["fonts/northwind-sans-800.woff2"] = FontFixtures.Normal(),
            ["fonts/northwind-text-400.woff2"] = FontFixtures.Normal(),
            ["fonts/northwind-mono-400.woff2"] = FontFixtures.Normal(),
            ["logo-light.svg"] = Svg(10),
            ["logo-dark.svg"] = Svg(20),
            ["art.svg"] = Svg(30),
            ["layouts/stage-ladder.html"] = Encoding.UTF8.GetBytes(LAYOUT_HTML),
            ["layouts/stage-ladder.css"] = Encoding.UTF8.GetBytes(LAYOUT_CSS),
            ["layouts/stage-ladder.json"] = Encoding.UTF8.GetBytes(LAYOUT_SLOTS),
        };
        foreach (var (path, bytes) in replace ?? [])
        {
            files[path] = bytes;
        }
        foreach (var path in without ?? [])
        {
            files.Remove(path);
        }
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, bytes) in files)
            {
                var entry = AddBytes(zip, path, bytes);
                if (path == linked)
                {
                    entry.ExternalAttributes = (0xA000 | 0x1FF) << 16;
                }
            }
            extra?.Invoke(zip);
        }
        return stream.ToArray();
    }

    private static ZipArchiveEntry AddBytes(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes);
        return entry;
    }

    private static byte[] ZipOf(IReadOnlyDictionary<string, byte[]> files)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, bytes) in files)
            {
                AddBytes(zip, path, bytes);
            }
        }
        return stream.ToArray();
    }

    private static Dictionary<string, byte[]> ReadZip(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return zip.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var stream = e.Open();
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        });
    }

    // Rewrites the uncompressed size the central directory claims for one entry (the field 24 bytes into its record).
    private static byte[] DeclareSize(byte[] zip, string name, uint declared)
    {
        var copy = (byte[])zip.Clone();
        var nameBytes = Encoding.UTF8.GetBytes(name);
        for (var i = 0; i + 46 + nameBytes.Length <= copy.Length; i++)
        {
            if (copy[i] == 0x50 && copy[i + 1] == 0x4B && copy[i + 2] == 1 && copy[i + 3] == 2
                && copy.AsSpan(i + 46, nameBytes.Length).SequenceEqual(nameBytes))
            {
                BitConverter.TryWriteBytes(copy.AsSpan(i + 24, 4), declared);
                return copy;
            }
        }
        throw new InvalidOperationException($"No central directory record for {name}.");
    }

    private static byte[] Svg(int size)
        => Encoding.UTF8.GetBytes($"""<svg xmlns="{SVG_NAMESPACE}" viewBox="0 0 {size} {size}"><rect width="{size}" height="{size}"/></svg>""");
}
