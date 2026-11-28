using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.AppLinks;

// K8: the two app-link files, from config, on the public host and on the join host.
[TestFixture]
public class AppLinksTests : ApiTestFixture
{
    private const string JOIN = "http://join.example";
    private const string APPLE_ID = "ABCDE12345.app.slideworks.mobile";
    private const string FINGERPRINT_A = "14:6D:E9:83:C5:73:06:50:D8:EE:B9:95:2F:34:FC:64:14:6D:E9:83:C5:73:06:50:D8:EE:B9:95:2F:34:FC:64";
    private const string FINGERPRINT_B = "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99";

    protected override Dictionary<string, string?> ExtraConfig => new()
    {
        ["SLIDEWORKS_JOIN_URL"] = JOIN,
        ["SLIDEWORKS_APPLE_APP_ID"] = APPLE_ID,
        ["SLIDEWORKS_ANDROID_CERT_SHA256"] = $"{FINGERPRINT_A}, {FINGERPRINT_B}",
    };

    [TestCase("")]
    [TestCase(JOIN)]
    public async Task AppleAppSiteAssociation_WhenConfigured_ShouldServeJsonWithoutRedirect(string host)
    {
        using var client = ClientFor(TestUser.Anonymous);

        var response = await client.GetAsync($"{host}/.well-known/apple-app-site-association");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Test]
    public async Task AppleAppSiteAssociation_WhenConfigured_ShouldClaimJoinPaths()
    {
        using var client = ClientFor(TestUser.Anonymous);

        var body = await client.GetFromJsonAsync<JsonElement>($"{JOIN}/.well-known/apple-app-site-association");

        var detail = body.GetProperty("applinks").GetProperty("details")[0];
        detail.GetProperty("appIDs").EnumerateArray().Select(id => id.GetString()).ShouldBe([APPLE_ID]);
        detail.GetProperty("components").EnumerateArray().Select(c => c.GetProperty("/").GetString()).ShouldBe(["/j/*", "/??????"]);
    }

    [Test]
    public async Task AppleAppSiteAssociation_WhenOnPublicHost_ShouldClaimOnlyJoinPaths()
    {
        using var client = ClientFor(TestUser.Anonymous);

        var body = await client.GetFromJsonAsync<JsonElement>($"{PublicUrl}/.well-known/apple-app-site-association");

        body.GetProperty("applinks").GetProperty("details")[0].GetProperty("components").EnumerateArray()
            .Select(c => c.GetProperty("/").GetString()).ShouldBe(["/j/*"]);
    }

    [Test]
    public async Task AppleAppSiteAssociation_WhenOnJoinHost_ShouldAlsoClaimBareCodes()
    {
        using var client = ClientFor(TestUser.Anonymous);

        var body = await client.GetFromJsonAsync<JsonElement>($"{JOIN}/.well-known/apple-app-site-association");

        body.GetProperty("applinks").GetProperty("details")[0].GetProperty("components").EnumerateArray()
            .Select(c => c.GetProperty("/").GetString()).ShouldBe(["/j/*", "/??????"]);
    }

    [TestCase("")]
    [TestCase(JOIN)]
    public async Task AssetLinks_WhenConfigured_ShouldServePackageAndFingerprints(string host)
    {
        using var client = ClientFor(TestUser.Anonymous);

        var response = await client.GetAsync($"{host}/.well-known/assetlinks.json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var target = body[0].GetProperty("target");
        target.GetProperty("namespace").GetString().ShouldBe("android_app");
        target.GetProperty("package_name").GetString().ShouldBe("app.slideworks.mobile");
        target.GetProperty("sha256_cert_fingerprints").EnumerateArray().Select(f => f.GetString()).ShouldBe([FINGERPRINT_A, FINGERPRINT_B]);
        body[0].GetProperty("relation")[0].GetString().ShouldBe("delegate_permission/common.handle_all_urls");
    }

    [Test]
    public async Task WellKnown_WhenOnJoinHost_ShouldServeOnlyTheAppLinkFiles()
    {
        using var client = ClientFor(TestUser.Anonymous);

        var oauthPaths = new[] { "oauth-authorization-server", "oauth-protected-resource", "oauth-protected-resource/mcp" };
        var oauth = new List<HttpResponseMessage>();
        foreach (var path in oauthPaths)
        {
            oauth.Add(await client.GetAsync($"{JOIN}/.well-known/{path}"));
        }
        var aasa = await client.GetAsync($"{JOIN}/.well-known/apple-app-site-association");
        var assetLinks = await client.GetAsync($"{JOIN}/.well-known/assetlinks.json");

        oauth.Select(r => r.StatusCode).ShouldBe([HttpStatusCode.Redirect, HttpStatusCode.Redirect, HttpStatusCode.Redirect]);
        oauth.Select(r => r.Headers.Location!.OriginalString).ShouldBe(oauthPaths.Select(path => $"{PublicUrl}/.well-known/{path}"));
        aasa.StatusCode.ShouldBe(HttpStatusCode.OK);
        assetLinks.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

// With neither setting set, both files are 404, on both hosts.
[TestFixture]
public class AppLinksNotConfiguredTests : ApiTestFixture
{
    private const string JOIN = "http://join.example";

    protected override Dictionary<string, string?> ExtraConfig => new() { ["SLIDEWORKS_JOIN_URL"] = JOIN };

    [TestCase("", "apple-app-site-association")]
    [TestCase("", "assetlinks.json")]
    [TestCase(JOIN, "apple-app-site-association")]
    [TestCase(JOIN, "assetlinks.json")]
    public async Task AppLinks_WhenNotConfigured_ShouldReturn404(string host, string file)
    {
        using var client = ClientFor(TestUser.Anonymous);

        var response = await client.GetAsync($"{host}/.well-known/{file}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

// One malformed fingerprint makes the whole setting count as not configured: assetlinks.json is 404, never a crash.
[TestFixture]
public class AppLinksInvalidFingerprintTests : ApiTestFixture
{
    private const string VALID = "14:6D:E9:83:C5:73:06:50:D8:EE:B9:95:2F:34:FC:64:14:6D:E9:83:C5:73:06:50:D8:EE:B9:95:2F:34:FC:64";
    private const string SHORT = "14:6D:E9:83:C5:73:06:50:D8:EE:B9:95:2F:34:FC:64";

    protected override Dictionary<string, string?> ExtraConfig => new() { ["SLIDEWORKS_ANDROID_CERT_SHA256"] = $"{VALID}, {SHORT}" };

    [Test]
    public async Task AssetLinks_WhenAFingerprintIsInvalid_ShouldReturn404()
    {
        using var client = ClientFor(TestUser.Anonymous);

        var response = await client.GetAsync("/.well-known/assetlinks.json");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

// Valid fingerprints are served upper-cased, in the order listed.
[TestFixture]
public class AppLinksFingerprintFormatTests : ApiTestFixture
{
    private const string LOWER = "aa:bb:cc:dd:ee:ff:00:11:22:33:44:55:66:77:88:99:aa:bb:cc:dd:ee:ff:00:11:22:33:44:55:66:77:88:99";

    protected override Dictionary<string, string?> ExtraConfig => new() { ["SLIDEWORKS_ANDROID_CERT_SHA256"] = LOWER };

    [Test]
    public async Task AssetLinks_WhenFingerprintIsLowerCase_ShouldServeItUpperCase()
    {
        using var client = ClientFor(TestUser.Anonymous);

        var body = await client.GetFromJsonAsync<JsonElement>("/.well-known/assetlinks.json");

        body[0].GetProperty("target").GetProperty("sha256_cert_fingerprints").EnumerateArray().Select(f => f.GetString()).ShouldBe([LOWER.ToUpperInvariant()]);
    }
}
