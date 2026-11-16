using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Dtos;
using SlideWorks.WebApi.Tests.Identity;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Accounts.Admin;

// The admin users API (S5). Ada is the admin, and every test that changes someone's access uses its own NewPending()
// person, so the tests don't depend on each other's order.
[TestFixture]
public class AdminUsersTests : ApiTestFixture
{
    private const string USERS = "/api/admin/users";

    protected override TestUser SignedInAs => TestUser.Ada;

    [Test]
    public async Task List_ShouldMatchScreenShape()
    {
        var pat = await PendingPersonAsync();

        var response = await Client.GetAsync(USERS);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var view = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonShape.AssertMatches(view, "admin-users.json", ScreenShapes.AdminUsers);
        var row = RowFor(view, pat.Id);
        row.GetProperty("email").GetString().ShouldBe(pat.Email);
        row.GetProperty("access").GetString().ShouldBe("pending");
        row.GetProperty("requestedLabel").GetString().ShouldBe("just now");
        row.GetProperty("changedLabel").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("changedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        view.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("isSelf").GetBoolean())
            .GetProperty("email").GetString().ShouldBe(TestUser.Ada.Email);
    }

    [Test]
    public async Task List_ShouldOrderPendingFirst()
    {
        var approved = await PendingPersonAsync();
        var rejected = await PendingPersonAsync();
        await PostAsync(approved.Id, "approve");
        await PostAsync(rejected.Id, "reject");
        await PendingPersonAsync();

        var view = await (await Client.GetAsync(USERS)).Content.ReadFromJsonAsync<JsonElement>();

        var order = view.GetProperty("users").EnumerateArray().Select(u => u.GetProperty("access").GetString()!).ToList();
        order.ShouldBe(order.OrderBy(a => a switch { "pending" => 0, "approved" => 1, _ => 2 }).ToList());
        order.ShouldContain("pending");
        order.ShouldContain("rejected");
        view.GetProperty("pendingCount").GetInt32().ShouldBe(order.Count(a => a == "pending"));
        view.GetProperty("adminCount").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task Approve_WhenPending_ShouldLetThemUseTheApi()
    {
        var pat = await PendingPersonAsync();
        using var asPat = ClientFor(pat.Person);
        (await asPat.GetAsync("/api/decks")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var response = await PostAsync(pat.Id, "approve");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = RowFor(await response.Content.ReadFromJsonAsync<JsonElement>(), pat.Id);
        row.GetProperty("access").GetString().ShouldBe("approved");
        row.GetProperty("changedBy").GetString().ShouldBe(TestUser.Ada.Name);
        (await asPat.GetAsync("/api/decks")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Reject_WhenPending_ShouldSignThemOut()
    {
        var pat = await PendingPersonAsync();
        using var asPat = ClientFor(pat.Person);

        var response = await PostAsync(pat.Id, "reject");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        RowFor(await response.Content.ReadFromJsonAsync<JsonElement>(), pat.Id).GetProperty("access").GetString().ShouldBe("rejected");
        (await asPat.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Approve_WhenRejected_ShouldLetThemBackIn()
    {
        var pat = await PendingPersonAsync();
        await PostAsync(pat.Id, "reject");

        var response = await PostAsync(pat.Id, "approve");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var asPat = ClientFor(pat.Person);
        (await asPat.GetAsync("/api/decks")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task MakeAdmin_ThenRemoveAdmin_ShouldRoundTrip()
    {
        var pat = await PendingPersonAsync();
        await PostAsync(pat.Id, "approve");

        var made = await (await PostAsync(pat.Id, "make-admin")).Content.ReadFromJsonAsync<JsonElement>();
        var removed = await (await PostAsync(pat.Id, "remove-admin")).Content.ReadFromJsonAsync<JsonElement>();

        RowFor(made, pat.Id).GetProperty("isAdmin").GetBoolean().ShouldBeTrue();
        made.GetProperty("adminCount").GetInt32().ShouldBe(2);
        RowFor(removed, pat.Id).GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
        removed.GetProperty("adminCount").GetInt32().ShouldBe(1);
    }

    [Test]
    public async Task RemoveAdmin_WhenLastAdmin_ShouldReturn409()
    {
        var response = await PostAsync(await AdaIdAsync(), "remove-admin");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorOfAsync(response)).ShouldBe("The last admin can't be removed");
    }

    [Test]
    public async Task Revoke_WhenLastAdmin_ShouldReturn409()
    {
        var response = await PostAsync(await AdaIdAsync(), "revoke");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorOfAsync(response)).ShouldBe("The last admin can't be removed");
    }

    [Test]
    public async Task Revoke_ShouldEndTheirCookieSession()
    {
        var pat = await PendingPersonAsync();
        await PostAsync(pat.Id, "approve");
        using var browser = Factory.CreateClient();
        browser.DefaultRequestHeaders.Add("Cookie", await SessionCookies.ForAsync(Factory, pat.Person));
        (await browser.GetAsync("/api/decks")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await PostAsync(pat.Id, "revoke");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = RowFor(await response.Content.ReadFromJsonAsync<JsonElement>(), pat.Id);
        row.GetProperty("access").GetString().ShouldBe("rejected");
        row.GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
        (await browser.GetAsync("/api/decks")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Revoke_ShouldStopTheirAgent()
    {
        var pat = await PendingPersonAsync();
        await PostAsync(pat.Id, "approve");
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, pat.Person);
        (await InitializeAsync(agent.Tokens.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await PostAsync(pat.Id, "revoke");
        (await InitializeAsync(agent.Tokens.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await PostAsync(pat.Id, "approve");

        (await InitializeAsync(agent.Tokens.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Action_WhenUnknownUser_ShouldReturn404()
    {
        foreach (var action in new[] { "approve", "reject", "make-admin", "remove-admin", "revoke" })
        {
            var response = await PostAsync(Guid.NewGuid(), action);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, action);
            (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("User not found");
        }
    }

    [TestCase("approve", true, "Only a pending or rejected person can be approved")]
    [TestCase("reject", true, "Only a pending person can be rejected")]
    [TestCase("make-admin", false, "Only an approved person who isn't an admin can be made an admin")]
    [TestCase("remove-admin", false, "That person isn't an admin")]
    [TestCase("revoke", false, "Only an approved person can be revoked")]
    public async Task Action_WhenNotAllowed_ShouldReturn409WithSentence(string action, bool approvedFirst, string sentence)
    {
        var pat = await PendingPersonAsync();
        if (approvedFirst)
        {
            await PostAsync(pat.Id, "approve");
        }

        var response = await PostAsync(pat.Id, action);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ErrorOfAsync(response)).ShouldBe(sentence);
    }

    [Test]
    public async Task Admin_WhenNotAdmin_ShouldReturn403()
    {
        var pat = await PendingPersonAsync();
        using var sam = ClientFor(TestUser.Sam);

        var list = await sam.GetAsync(USERS);
        var approve = await sam.PostAsync($"{USERS}/{pat.Id}/approve", content: null);

        list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await list.Content.ReadFromJsonAsync<ErrorDto>())!.Error.ShouldBe("Admins only");
        approve.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Client.GetFromJsonAsync<JsonElement>(USERS)).GetProperty("users").EnumerateArray()
            .Single(u => u.GetProperty("id").GetGuid() == pat.Id).GetProperty("access").GetString().ShouldBe("pending");
    }

    private async Task<PendingPerson> PendingPersonAsync()
    {
        var person = TestUser.NewPending();
        using var scope = Factory.Services.CreateScope();
        var user = await TestPeople.SignInAsync(scope.ServiceProvider, person);
        return new PendingPerson(person, user.Id);
    }

    private sealed record PendingPerson(TestUser Person, Guid Id)
    {
        public string Email => Person.Email;
    }

    private async Task<Guid> AdaIdAsync()
    {
        var view = await Client.GetFromJsonAsync<JsonElement>(USERS);
        return view.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("isSelf").GetBoolean()).GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> PostAsync(Guid id, string action) => Client.PostAsync($"{USERS}/{id}/{action}", content: null);

    private static JsonElement RowFor(JsonElement view, Guid id)
        => view.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == id);

    private static async Task<string> ErrorOfAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ErrorDto>())!.Error;

    // A bare client, so only the bearer token is in play.
    private async Task<HttpResponseMessage> InitializeAsync(string accessToken)
    {
        using var http = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await http.SendAsync(request);
    }
}
