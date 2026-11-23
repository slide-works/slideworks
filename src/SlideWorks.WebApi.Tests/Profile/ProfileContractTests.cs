using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Profile;

// The profile wire's shape and gates (C3, C9). The stand-in seams answer 500 until wave 1, so no test here calls one.
[TestFixture]
public class ProfileContractTests : ApiTestFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => FakeConnectedAgents.AddTo;

    [Test]
    public async Task GetProfile_WhenApproved_ShouldMatchProfileShape()
    {
        var response = await Client.GetAsync("/api/me/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(profile, "profile.json", ScreenShapes.Profile);
        profile.TryGetProperty("people", out _).ShouldBeFalse();
        var identity = profile.GetProperty("identity");
        identity.GetProperty("name").GetString().ShouldBe(TestUser.Sam.Name);
        identity.GetProperty("email").GetString().ShouldBe(TestUser.Sam.Email);
        identity.GetProperty("avatar").GetProperty("initials").GetString().ShouldBe("SR");
        identity.GetProperty("colour").ValueKind.ShouldBe(JsonValueKind.Null);
        identity.GetProperty("colours").GetArrayLength().ShouldBe(8);
        identity.GetProperty("hasPhoto").GetBoolean().ShouldBeFalse();
        var account = profile.GetProperty("account");
        account.GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
        account.GetProperty("adminUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        account.GetProperty("timeZones").EnumerateArray().Select(z => z.GetProperty("id").GetString()).ShouldContain("Europe/London");
        var agents = profile.GetProperty("agents").EnumerateArray().ToList();
        agents.Select(a => a.GetProperty("clientName").GetString()).ShouldBe(["Claude", "Cursor"]);
        agents.Select(a => a.GetProperty("connectedLabel").GetString()).ShouldBe(["3 days ago", "Unknown"]);
        agents[1].GetProperty("lastUsedLabel").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task GetProfile_WhenAdmin_ShouldCarryTheAdminUrl()
    {
        using var ada = ClientFor(TestUser.Ada);

        var profile = await ada.GetFromJsonAsync<JsonElement>("/api/me/profile");

        var account = profile.GetProperty("account");
        account.GetProperty("isAdmin").GetBoolean().ShouldBeTrue();
        account.GetProperty("adminUrl").GetString().ShouldBe("/admin/users");
        JsonShape.AssertMatches(profile, "profile.json", ScreenShapes.Profile);
    }

    [Test]
    public async Task GetProfile_WhenPending_ShouldReturn403PendingApproval()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync("/api/me/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("pending_approval");
    }

    [Test]
    public async Task PutProfile_WhenPending_ShouldReturn403PendingApproval()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.PutAsJsonAsync("/api/me/profile", new { name = "Pat", colour = "teal" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("pending_approval");
    }

    [Test]
    public async Task GetProfile_WhenAnonymous_ShouldReturn401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync("/api/me/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetPhoto_WhenUnknownPhotoId_ShouldReturn404()
    {
        var response = await Client.GetAsync($"/api/people/{Guid.NewGuid()}/photo/nope");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetPhoto_WhenPendingAndUnknownPhotoId_ShouldReturn404NotTheApprovalGate()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.GetAsync($"/api/people/{Guid.NewGuid()}/photo/nope");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetPhoto_WhenAnonymous_ShouldReturn401()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.GetAsync($"/api/people/{Guid.NewGuid()}/photo/nope");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task DeleteAgent_WhenNotALiveFamilyOfTheCaller_ShouldReturn404()
    {
        var response = await Client.DeleteAsync($"/api/me/agents/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteAgent_WhenPending_ShouldReturn403PendingApproval()
    {
        using var pending = ClientFor(TestUser.NewPending());

        var response = await pending.DeleteAsync($"/api/me/agents/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Unsubscribe_WhenAnonymous_ShouldReachTheRoute()
    {
        using var anonymous = ClientFor(TestUser.Anonymous);

        var response = await anonymous.PostAsJsonAsync("/api/email/unsubscribe", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
