using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.Accounts.Avatars;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Sharing.Email;
using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// The sharing wire (C4), IsOwnerAsync (C5) and the write path's order (C6). The fixture swaps IShareGuard and IShareNotifier
// for fakes that record what they were given and refuse the emails a test lists; every test uses emails of its own.
[TestFixture]
public class SharingContractTests : ApiTestFixture
{
    private static readonly RecordingGuard GUARD = new();
    private static readonly RecordingNotifier NOTIFIER = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IShareGuard>(GUARD);
        services.AddSingleton<IShareNotifier>(NOTIFIER);
    };

    [Test]
    public async Task Get_WhenOwner_ShouldMatchSharingShapeWithOwnerAndPeople()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var alex = $"shape-{Guid.NewGuid():N}@team.example";
        var stranger = $"nobody-{Guid.NewGuid():N}@nowhere.example";
        await PutPeopleAsync(deck, "named", (alex, "write"), (stranger, "read"));

        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        JsonShape.AssertMatches(view, "sharing.json", ScreenShapes.Sharing);
        view.GetProperty("people").EnumerateArray().Select(p => (p.GetProperty("email").GetString(), p.GetProperty("access").GetString()))
            .ShouldBe([(alex, "write"), (stranger, "read")], ignoreOrder: true);
        view.GetProperty("people").EnumerateArray().All(p => p.GetProperty("name").ValueKind == JsonValueKind.Null).ShouldBeTrue();
    }

    [Test]
    public async Task Get_WhenOwner_ShouldReturnOwnersEmailNameAndAvatar()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        var owner = view.GetProperty("owner");
        owner.GetProperty("email").GetString().ShouldBe(TestUser.Sam.Email);
        owner.GetProperty("name").GetString().ShouldBe(TestUser.Sam.Name);
        var avatar = owner.GetProperty("avatar");
        avatar.GetProperty("initials").GetString().ShouldBe("SR");
        AvatarColours.Keys.ShouldContain(avatar.GetProperty("colour").GetString()!);
        avatar.GetProperty("pictureUrl").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Get_WhenNewDeck_ShouldHaveNoPeople()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        view.GetProperty("people").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task Put_WhenViewersOnly_ShouldStoreReadForEach()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var one = $"v1-{Guid.NewGuid():N}@team.example";
        var two = $"v2-{Guid.NewGuid():N}@team.example";

        var view = await SharingApi.PutOkAsync(Client, deck, "named", one, two);

        AccessOf(view).ShouldBe([(one, "read"), (two, "read")], ignoreOrder: true);
        view.GetProperty("viewers").GetArrayLength().ShouldBe(2);
    }

    [Test]
    public async Task Put_WhenPeopleWithWrite_ShouldStoreWrite()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var editor = $"w1-{Guid.NewGuid():N}@team.example";
        var reader = $"w2-{Guid.NewGuid():N}@team.example";

        var view = await PutPeopleAsync(deck, "named", (editor, "write"), (reader, "read"));
        var again = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        AccessOf(view).ShouldBe([(editor, "write"), (reader, "read")], ignoreOrder: true);
        AccessOf(again).ShouldBe(AccessOf(view), ignoreOrder: true);
        view.GetProperty("viewers").GetArrayLength().ShouldBe(2);
    }

    [Test]
    public async Task Put_WhenAccessChangesOnly_ShouldStoreTheNewAccess()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var person = $"flip-{Guid.NewGuid():N}@team.example";
        await PutPeopleAsync(deck, "named", (person, "read"));

        var view = await PutPeopleAsync(deck, "named", (person, "write"));

        AccessOf(view).ShouldBe([(person, "write")]);
    }

    [Test]
    public async Task Put_WhenGuardRefuses_ShouldStoreNothingAndReturn429()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var kept = $"kept-{Guid.NewGuid():N}@team.example";
        await PutPeopleAsync(deck, "named", (kept, "read"));
        var activityBefore = (await DeckSeed.ActivityAsync(Client, deck)).Count;
        var refused = $"refused-{Guid.NewGuid():N}@team.example";
        GUARD.Refuse(refused, TimeSpan.FromSeconds(125));

        var response = await SharingApi.PutAsync(Client, deck, new { level = "link", people = new[] { new { email = kept, access = "read" }, new { email = refused, access = "write" } } });

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter!.Delta.ShouldBe(TimeSpan.FromSeconds(125));
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe(RecordingGuard.MESSAGE);
        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        view.GetProperty("level").GetString().ShouldBe("named");
        AccessOf(view).ShouldBe([(kept, "read")]);
        (await DeckSeed.ActivityAsync(Client, deck)).Count.ShouldBe(activityBefore);
        NOTIFIER.To(refused).ShouldBeEmpty();
        (await RecordedAsync(refused)).ShouldBeFalse();
    }

    [Test]
    public async Task Put_WhenGuardAdmits_ShouldPassAddedOnlyToGuardAndNotifier()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var existing = $"old-{Guid.NewGuid():N}@team.example";
        var fresh = $"new-{Guid.NewGuid():N}@Team.Example";
        await PutPeopleAsync(deck, "named", (existing, "read"));

        await PutPeopleAsync(deck, "named", (existing, "write"), (fresh, "read"));

        var freshStored = fresh.ToLowerInvariant();
        GUARD.CallsWith(freshStored).ShouldHaveSingleItem().ShouldBe([freshStored]);
        GUARD.CallsWith(existing).Count.ShouldBe(1, "only the first share added them");
        NOTIFIER.To(freshStored).ShouldHaveSingleItem().DeckId.ShouldBe(deck);
        NOTIFIER.To(freshStored)[0].Added.ShouldBe([freshStored]);
        (await RecordedAsync(freshStored)).ShouldBeTrue();
    }

    [Test]
    public async Task Put_WhenOnlyLevelChangesOrSomeoneIsRemoved_ShouldAdmitAndNotifyNobody()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var person = $"stay-{Guid.NewGuid():N}@team.example";
        await PutPeopleAsync(deck, "named", (person, "read"));
        var guardCalls = GUARD.CallsWith(person).Count;
        var notices = NOTIFIER.To(person).Count;

        await PutPeopleAsync(deck, "link", (person, "read"));
        await PutPeopleAsync(deck, "link");

        GUARD.CallsWith(person).Count.ShouldBe(guardCalls);
        NOTIFIER.To(person).Count.ShouldBe(notices);
    }

    [Test]
    public async Task Put_WhenPeopleAndViewersBoth_ShouldUsePeople()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var person = $"both-{Guid.NewGuid():N}@team.example";

        var response = await SharingApi.PutAsync(Client, deck, new { level = "named", viewers = new[] { "ignored@team.example" }, people = new[] { new { email = person, access = "write" } } });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AccessOf(await response.Content.ReadFromJsonAsync<JsonElement>()).ShouldBe([(person, "write")]);
    }

    [TestCase("""{ "level": "named", "people": [{ "email": "a@team.example", "access": "admin" }] }""")]
    [TestCase("""{ "level": "named", "people": [{ "email": "a@team.example" }] }""")]
    [TestCase("""{ "level": "named", "people": "a@team.example" }""")]
    public async Task Put_WhenPeopleMalformed_ShouldReturn400AndStoreNothing(string json)
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await Client.PutAsync($"/api/decks/{deck}/sharing", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().ShouldBe(SharingService.PEOPLE_INVALID);
    }

    [Test]
    public async Task Put_WhenPersonIsNotAFullEmail_ShouldReturn400AndStoreNothing()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);

        var response = await SharingApi.PutAsync(Client, deck, new { level = "named", people = new[] { new { email = "alex", access = "read" } } });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing")).GetProperty("people").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task IsOwnerAsync_WhenAgentOfOwner_ShouldBeTrue()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        using var scope = Factory.Services.CreateScope();
        var sam = await TestPeople.SignInAsync(scope.ServiceProvider, TestUser.Sam);

        var isOwner = await scope.ServiceProvider.GetRequiredService<IDeckAccess>().IsOwnerAsync(deck, new StubActor(sam.Id, sam.Email, ActorKind.Agent));

        isOwner.ShouldBeTrue();
    }

    [Test]
    public async Task IsOwnerAsync_WhenNamedViewer_ShouldBeFalse()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        await PutPeopleAsync(deck, "named", (TestUser.Alex.Email, "write"));
        using var scope = Factory.Services.CreateScope();
        var alex = await TestPeople.SignInAsync(scope.ServiceProvider, TestUser.Alex);
        var access = scope.ServiceProvider.GetRequiredService<IDeckAccess>();

        var asPerson = await access.IsOwnerAsync(deck, new StubActor(alex.Id, alex.Email, ActorKind.Person));
        var asAgent = await access.IsOwnerAsync(deck, new StubActor(alex.Id, alex.Email, ActorKind.Agent));
        var unknown = await access.IsOwnerAsync("nOsUcHdEcK00", new StubActor(alex.Id, alex.Email, ActorKind.Person));

        (asPerson, asAgent, unknown).ShouldBe((false, false, false));
    }

    [Test]
    public async Task SetSharing_WhenGuardRefuses_ShouldReturnToolErrorWithTheMessageAndStoreNothing()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var refused = $"mcp-refused-{Guid.NewGuid():N}@team.example";
        GUARD.Refuse(refused, TimeSpan.FromSeconds(60));

        var (isError, result) = await agent.CallToolAsync("set_sharing", new { deck, level = "named", viewers = new[] { refused } });

        isError.ShouldBeTrue();
        result.GetString().ShouldBe(RecordingGuard.MESSAGE);
        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        view.GetProperty("level").GetString().ShouldBe("private");
        view.GetProperty("viewers").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task SetSharing_WhenLevelOmitted_ShouldKeepTheLevelAndChangeTheViewers()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await SharingApi.PutOkAsync(Client, deck, "link");
        var viewer = $"mcp-keep-{Guid.NewGuid():N}@team.example";

        var (isError, result) = await agent.CallToolAsync("set_sharing", new { deck, viewers = new[] { viewer } });

        isError.ShouldBeFalse(result.ToString());
        result.GetProperty("level").GetString().ShouldBe("link");
        AccessOf(result).ShouldBe([(viewer, "read")]);
    }

    [Test]
    public async Task SetSharing_WhenPersonGiven_ShouldSayNotSupportedYet()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam, "Claude");
        var deck = await DeckSeed.CreateDeckAsync(agent);

        var (isError, result) = await agent.CallToolAsync("set_sharing", new { deck, person = "alex@team.example", access = "write" });

        isError.ShouldBeTrue();
        result.GetString().ShouldBe("person: not supported yet");
    }

    private async Task<JsonElement> PutPeopleAsync(string deck, string level, params (string Email, string Access)[] people)
    {
        var response = await SharingApi.PutAsync(Client, deck, new { level, people = people.Select(p => new { email = p.Email, access = p.Access }).ToArray() });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static IReadOnlyList<(string Email, string Access)> AccessOf(JsonElement view)
        => view.GetProperty("people").EnumerateArray().Select(p => (p.GetProperty("email").GetString()!, p.GetProperty("access").GetString()!)).ToList();

    private async Task<bool> RecordedAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var sam = await TestPeople.SignInAsync(scope.ServiceProvider, TestUser.Sam);
        var recent = await scope.ServiceProvider.GetRequiredService<IShareRecipients>().DistinctSinceAsync(sam.Id, DateTimeOffset.MinValue);
        return recent.Any(r => r.Email == ViewerEmails.Normalize(email));
    }

    private sealed record StubActor(Guid Id, string AddressOf, ActorKind Of) : ICurrentActor
    {
        public bool IsSignedIn => true;

        public Guid? PresenterId => Id;

        public string? Email => AddressOf;

        public string? Name => "Stub";

        public ActorKind Kind => Of;

        public ActorVia Via => Of == ActorKind.Agent ? ActorVia.Mcp : ActorVia.Web;

        public UserAccess? Access => UserAccess.Approved;

        public bool IsAdmin => false;
    }

    private sealed class RecordingGuard : IShareGuard
    {
        public const string MESSAGE = "You can share with 10 new people an hour. Try again at 15:42.";

        private readonly ConcurrentDictionary<string, TimeSpan> _refused = new();
        private readonly ConcurrentQueue<IReadOnlyList<string>> _calls = new();

        public void Refuse(string email, TimeSpan retryAfter) => _refused[ViewerEmails.Normalize(email)] = retryAfter;

        public IReadOnlyList<IReadOnlyList<string>> CallsWith(string email)
            => [.. _calls.Where(call => call.Contains(ViewerEmails.Normalize(email)))];

        public Task AdmitAsync(Guid sharerId, IReadOnlyList<string> newlyAdded)
        {
            _calls.Enqueue(newlyAdded);
            foreach (var email in newlyAdded)
            {
                if (_refused.TryGetValue(email, out var retryAfter))
                {
                    throw new TooManyRequestsException(MESSAGE, retryAfter);
                }
            }
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotifier : IShareNotifier
    {
        private readonly ConcurrentQueue<ShareNotice> _notices = new();

        public IReadOnlyList<ShareNotice> To(string email)
            => [.. _notices.Where(notice => notice.Added.Contains(ViewerEmails.Normalize(email)))];

        public Task QueueAsync(ShareNotice notice)
        {
            _notices.Enqueue(notice);
            return Task.CompletedTask;
        }
    }
}
