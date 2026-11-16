using System.Net;
using System.Text.Json;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Metadata;

// The discovery an agent does with nothing but the /mcp URL (D11): /mcp's 401 names the protected-resource document,
// which names the authorization server, whose document lists the endpoints. Anonymous, and always the configured URL.
[TestFixture]
public class MetadataTests : ApiTestFixture
{
    private const string PROTECTED_RESOURCE = "/.well-known/oauth-protected-resource";
    private const string AUTHORIZATION_SERVER = "/.well-known/oauth-authorization-server";

    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    public async Task ProtectedResource_ShouldPointAtConfiguredIssuer()
    {
        var metadata = await GetJsonAsync(PROTECTED_RESOURCE);

        metadata.GetProperty("resource").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/mcp");
        metadata.GetProperty("authorization_servers").EnumerateArray().Select(s => s.GetString()).ShouldBe([TestAppFactory.PUBLIC_URL]);
        metadata.GetProperty("scopes_supported").EnumerateArray().Select(s => s.GetString()).ShouldBe(["decks"]);
    }

    // The URL /mcp's 401 points at is this document, and the RFC 9728 path-inserted form serves the same one.
    [Test]
    public async Task ProtectedResource_WhenReachedFromMcp401_ShouldServeTheDocument()
    {
        var challenge = await Client.PostAsync("/mcp", content: null);
        var header = challenge.Headers.WwwAuthenticate.ToString();
        var url = header[(header.IndexOf("resource_metadata=\"", StringComparison.Ordinal) + "resource_metadata=\"".Length)..].TrimEnd('"');

        url.ShouldBe($"{TestAppFactory.PUBLIC_URL}{PROTECTED_RESOURCE}");
        (await GetJsonAsync(new Uri(url).PathAndQuery)).GetProperty("resource").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/mcp");
        (await GetJsonAsync(PROTECTED_RESOURCE + "/mcp")).GetProperty("resource").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/mcp");
    }

    [TestCase(PROTECTED_RESOURCE)]
    [TestCase(AUTHORIZATION_SERVER)]
    public async Task Metadata_WhenHostHeaderForged_ShouldStillUseConfiguredUrl(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "evil.example";

        var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("evil.example", Case.Insensitive);
        body.ShouldContain($"\"{TestAppFactory.PUBLIC_URL}", Case.Sensitive);
    }

    [Test]
    public async Task AuthorizationServer_ShouldAdvertiseS256AndEndpoints()
    {
        var metadata = await GetJsonAsync(AUTHORIZATION_SERVER);

        metadata.GetProperty("issuer").GetString().ShouldBe(TestAppFactory.PUBLIC_URL);
        metadata.GetProperty("authorization_endpoint").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/oauth/authorize");
        metadata.GetProperty("token_endpoint").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/oauth/token");
        metadata.GetProperty("registration_endpoint").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/oauth/register");
        metadata.GetProperty("revocation_endpoint").GetString().ShouldBe($"{TestAppFactory.PUBLIC_URL}/oauth/revoke");
        Values(metadata, "code_challenge_methods_supported").ShouldBe(["S256"]);
        Values(metadata, "scopes_supported").ShouldBe(["decks"]);
        Values(metadata, "token_endpoint_auth_methods_supported").ShouldBe(["none"]);
        Values(metadata, "response_types_supported").ShouldBe(["code"]);
        Values(metadata, "grant_types_supported").ShouldBe(["authorization_code", "refresh_token"]);
    }

    private static string?[] Values(JsonElement metadata, string name)
        => [.. metadata.GetProperty(name).EnumerateArray().Select(s => s.GetString())];

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        var response = await Client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
