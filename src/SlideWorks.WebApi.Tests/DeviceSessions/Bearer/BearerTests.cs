using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.DeviceSessions;
using SlideWorks.WebApi.Features.DeviceSessions.Data;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.DeviceSessions.Bearer;

// The device bearer on /api (K5): who it signs in, what it refuses with which body, how it stays apart from MCP tokens,
// the cookie's sign-out and the write guard, and DELETE /api/auth/device-sessions/current.
[TestFixture]
public class BearerTests : ApiTestFixture
{
    private const string ME = "/api/me";

    private const string CURRENT = "/api/auth/device-sessions/current";

    private DeviceSessionRowSeed Seed => new(Factory.Services);

    [Test]
    public async Task Bearer_WhenSecretUnknown_ShouldAnswerNotSignedIn()
    {
        using var bearer = BearerClient("swd_" + new string('A', 43));

        using var response = await bearer.GetAsync(ME);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ErrorOfAsync(response)).ShouldBe("Not signed in");
    }

    [Test]
    public async Task Bearer_WhenPending_ShouldGetPendingApproval()
    {
        var (_, secret) = await Seed.CreateAsync(TestUser.NewPending());
        using var bearer = BearerClient(secret);

        using var decks = await bearer.GetAsync("/api/decks");
        using var me = await bearer.GetAsync(ME);

        decks.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ErrorOfAsync(decks)).ShouldBe("pending_approval");
        me.StatusCode.ShouldBe(HttpStatusCode.OK, await me.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Bearer_WhenAdminRoute_ShouldMatchCookieForAdminAndNonAdmin()
    {
        var statuses = new List<HttpStatusCode>();

        foreach (var person in new[] { TestUser.Ada, TestUser.Mira })
        {
            var (_, secret) = await Seed.CreateAsync(person);
            using var bearer = BearerClient(secret);
            using var cookie = ClientFor(person);
            using var byCookie = await cookie.GetAsync("/api/admin/users");
            using var byBearer = await bearer.GetAsync("/api/admin/users");
            statuses.Add(byCookie.StatusCode);
            statuses.Add(byBearer.StatusCode);
        }

        statuses.ShouldBe([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden]);
    }

    [Test]
    public async Task Bearer_WhenPersonRejected_ShouldEndSessionAsAccessChanged()
    {
        var (session, secret) = await Seed.CreateAsync(TestUser.NewPending());
        using var bearer = BearerClient(secret);
        using var admin = ClientFor(TestUser.Ada);
        (await admin.PostAsync($"/api/admin/users/{session.UserId}/reject", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await bearer.GetAsync(ME);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ErrorOfAsync(response)).ShouldBe("session_ended");
        (await StoredAsync(secret)).EndReason.ShouldBe(DeviceSessionEndReason.AccessChanged);
    }

    [Test]
    public async Task Bearer_WhenOnMcp_ShouldBeRefused()
    {
        var (_, secret) = await Seed.CreateAsync(TestUser.Sam);
        using var bearer = BearerClient(secret);
        using var initialize = new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}""",
            Encoding.UTF8,
            "application/json");
        bearer.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");

        using var response = await bearer.PostAsync("/mcp", initialize);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ToString().ShouldContain("error=\"invalid_token\"", Case.Sensitive);
    }

    [Test]
    public async Task McpToken_WhenOnApi_ShouldBeRefused()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        using var bearer = BearerClient(agent.Tokens.AccessToken);

        using var response = await bearer.GetAsync(ME);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ErrorOfAsync(response)).ShouldBe("Not signed in");
    }

    [Test]
    public async Task Bearer_WhenAccessTokenQueryOnApi_ShouldBeIgnored()
    {
        var (_, secret) = await Seed.CreateAsync(TestUser.Sam);
        using var anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await anonymous.GetAsync($"{ME}?access_token={Uri.EscapeDataString(secret)}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ErrorOfAsync(response)).ShouldBe("Not signed in");
    }

    [Test]
    public async Task SignOut_WhenBearer_ShouldLeaveTheDeviceSessionLive()
    {
        var (_, secret) = await Seed.CreateAsync(TestUser.Sam);
        using var bearer = BearerClient(secret);

        using var signOut = await bearer.PostAsync("/api/signout", content: null);
        using var me = await bearer.GetAsync(ME);

        signOut.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        me.StatusCode.ShouldBe(HttpStatusCode.OK, await me.Content.ReadAsStringAsync());
        (await StoredAsync(secret)).EndedAt.ShouldBeNull();
    }

    [Test]
    public async Task CurrentDelete_WhenBearer_ShouldEndAsAppSignOut()
    {
        var (_, secret) = await Seed.CreateAsync(TestUser.Sam);
        using var bearer = BearerClient(secret);

        using var response = await bearer.DeleteAsync(CURRENT);
        using var me = await bearer.GetAsync(ME);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await StoredAsync(secret)).EndReason.ShouldBe(DeviceSessionEndReason.AppSignOut);
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await ErrorOfAsync(me)).ShouldBe("session_ended");
    }

    [Test]
    public async Task CurrentDelete_WhenCookie_ShouldReturn404NoDeviceSession()
    {
        using var response = await Client.DeleteAsync(CURRENT);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().ShouldBe("no_device_session");
        body.GetProperty("message").GetString().ShouldBe("This request isn't from a signed-in phone.");
    }

    // A write from another site's Origin is refused for the cookie and passes for a bearer, which no cookie rides on.
    [Test]
    public async Task CrossSiteWrite_WhenBearer_ShouldNotBeRefused()
    {
        var (_, secret) = await Seed.CreateAsync(TestUser.Sam);
        using var bearer = BearerClient(secret);
        using var cookie = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        cookie.DefaultRequestHeaders.Add(TestAuthScheme.HEADER, TestUser.Sam.Email);

        using var byBearer = await bearer.SendAsync(ForeignSignOut());
        using var byCookie = await cookie.SendAsync(ForeignSignOut());

        byBearer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        byCookie.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static HttpRequestMessage ForeignSignOut()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/signout");
        request.Headers.Add("Origin", "https://elsewhere.example");
        return request;
    }

    private HttpClient BearerClient(string secret)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return client;
    }

    private async Task<DeviceSession> StoredAsync(string secret)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IDeviceSessions>().FindBySecretAsync(secret)).ShouldNotBeNull();
    }

    private static async Task<string?> ErrorOfAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
}
