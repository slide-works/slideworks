using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Data;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.OAuth.Authorize;
using SlideWorks.WebApi.Tests.OAuth.Token;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.ConnectedAgents;

// The connected agents of /profile (C3, D18): one row per live token family of the caller, Disconnect revokes the
// family, and a family's last use is stamped at most once every five minutes. Agents connect through the OAuth routes.
[TestFixture]
public class ConnectedAgentsTests : ApiTestFixture
{
    private HttpClient _anonymous = null!;
    private TokenEndpoint _endpoint = null!;
    private string _clientId = null!;

    [OneTimeSetUp]
    public async Task RegisterClient()
    {
        _anonymous = ClientFor(TestUser.Anonymous);
        _endpoint = new TokenEndpoint(Factory, _anonymous);
        _clientId = await OAuthFlow.RegisterAsync(_anonymous, "Claude");
    }

    [OneTimeTearDown]
    public void DisposeAnonymous() => _anonymous.Dispose();

    [SetUp]
    public void NextWindow() => Clock.Advance(TimeSpan.FromMinutes(1));

    [Test]
    public async Task GetProfile_WhenAgentConnected_ShouldListItWithLabels()
    {
        var zedId = await OAuthFlow.RegisterAsync(_anonymous, "Zed");
        var (access, _) = await _endpoint.TokensAsync(zedId, TestUser.Mira);
        using var mira = ClientFor(TestUser.Mira);
        Clock.Advance(TimeSpan.FromMinutes(30));

        var before = (await AgentsAsync(mira)).Where(a => a.GetProperty("clientName").GetString() == "Zed").ToList();
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);
        var after = (await AgentsAsync(mira)).Where(a => a.GetProperty("clientName").GetString() == "Zed").ToList();

        before.Count.ShouldBe(1);
        before[0].GetProperty("clientName").GetString().ShouldBe("Zed");
        before[0].GetProperty("connectedLabel").GetString().ShouldBe("30 min ago");
        before[0].GetProperty("lastUsedLabel").ValueKind.ShouldBe(JsonValueKind.Null);
        after[0].GetProperty("lastUsedLabel").GetString().ShouldBe("just now");
    }

    [Test]
    public async Task GetProfile_WhenTokenIssuedAfterTheMigration_ShouldNotSayUnknown()
    {
        await _endpoint.TokensAsync(_clientId, TestUser.Alex);
        using var alex = ClientFor(TestUser.Alex);

        var agents = await AgentsAsync(alex);

        agents.ShouldNotBeEmpty();
        agents.ShouldAllBe(a => a.GetProperty("connectedLabel").GetString() != "Unknown");
    }

    [Test]
    public async Task GetProfile_WhenTokenRefreshed_ShouldKeepTheFirstConnectedTime()
    {
        var (_, refresh) = await _endpoint.TokensAsync(_clientId, TestUser.Ada);
        using var ada = ClientFor(TestUser.Ada);
        Clock.Advance(TimeSpan.FromHours(3));

        await TokenEndpoint.OkBodyAsync(await _endpoint.RefreshAsync(_clientId, refresh));

        var agents = await AgentsAsync(ada);
        agents.Count.ShouldBe(1);
        agents[0].GetProperty("connectedLabel").GetString().ShouldBe("3 h ago");
    }

    [Test]
    public async Task DeleteAgent_WhenOwnFamily_ShouldMakeNextMcpCall401()
    {
        var (access, refresh) = await _endpoint.TokensAsync(_clientId);
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);
        var familyId = (await AgentsAsync(Client)).Select(a => a.GetProperty("id").GetString()).First();

        var response = await Client.DeleteAsync($"/api/me/agents/{familyId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.Unauthorized);
        (await _endpoint.RefreshAsync(_clientId, refresh)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AgentsAsync(Client)).Select(a => a.GetProperty("id").GetString()).ShouldNotContain(familyId);
    }

    [Test]
    public async Task DeleteAgent_WhenOtherPersonsFamily_ShouldReturn404AndKeepIt()
    {
        var otherId = await OAuthFlow.RegisterAsync(_anonymous, "Other");
        var (access, _) = await _endpoint.TokensAsync(otherId, TestUser.Mira);
        using var mira = ClientFor(TestUser.Mira);
        var familyId = (await AgentsAsync(mira)).Single(a => a.GetProperty("clientName").GetString() == "Other").GetProperty("id").GetString();
        using var alex = ClientFor(TestUser.Alex);

        var response = await alex.DeleteAsync($"/api/me/agents/{familyId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _endpoint.McpStatusAsync(access)).ShouldBe(HttpStatusCode.OK);
        (await AgentsAsync(mira)).Select(a => a.GetProperty("id").GetString()).ShouldContain(familyId);
    }

    [Test]
    public async Task DeleteAgent_WhenFamilyAlreadyDisconnected_ShouldReturn404()
    {
        await _endpoint.TokensAsync(_clientId, TestUser.Ada);
        using var ada = ClientFor(TestUser.Ada);
        var familyId = (await AgentsAsync(ada))[0].GetProperty("id").GetString();
        (await ada.DeleteAsync($"/api/me/agents/{familyId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var again = await ada.DeleteAsync($"/api/me/agents/{familyId}");

        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Validate_WhenCalledTwiceInFiveMinutes_ShouldStampLastUsedOnce()
    {
        var (access, _) = await _endpoint.TokensAsync(_clientId, TestUser.Sam);

        await ValidateAsync(access);
        var first = await LastUsedAsync(access);
        Clock.Advance(TimeSpan.FromMinutes(4));
        await ValidateAsync(access);
        var second = await LastUsedAsync(access);
        Clock.Advance(TimeSpan.FromMinutes(2));
        await ValidateAsync(access);
        var third = await LastUsedAsync(access);

        first.ShouldNotBeNull();
        second.ShouldBe(first);
        third.ShouldBe(Clock.GetUtcNow());
    }

    private static async Task<List<JsonElement>> AgentsAsync(HttpClient client)
    {
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/me/profile");
        return [.. profile.GetProperty("agents").EnumerateArray()];
    }

    private async Task ValidateAsync(string accessToken)
    {
        using var scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IAgentTokens>().ValidateAsync(accessToken, OAuthFlow.Resource)).ShouldNotBeNull();
    }

    private async Task<DateTimeOffset?> LastUsedAsync(string accessToken)
    {
        using var scope = Factory.Services.CreateScope();
        var hash = SecretTokens.Hash(accessToken);
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AgentTokens.AsNoTracking()
            .Where(t => t.TokenHash == hash)
            .Select(t => t.LastUsedAt)
            .SingleAsync();
    }
}
