using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing;
using SlideWorks.WebApi.Features.Sharing.Email;
using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// Per-person access through the sharing PUT (C4, C6, D19, D24): the activity wording, no notice for a level change, names and
// avatars for people with an account, and S0's owner field and share log kept real. Every test uses emails of its own.
[TestFixture]
public class PeopleAccessTests : ApiTestFixture
{
    private static readonly RecordingNotifier NOTIFIER = new();
    private static readonly RecordingGuard GUARD = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IShareGuard>(GUARD);
        services.AddSingleton<IShareNotifier>(NOTIFIER);
        FakeMailSender.AddTo(services);
    };

    [Test]
    public async Task Put_WhenWriteGranted_ShouldSummariseAccessInActivity()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var alex = Fresh("alex");
        var mira = Fresh("mira");
        await PutPeopleAsync(deck, "named", (mira, "read"));

        await PutPeopleAsync(deck, "named", (alex, "write"), (mira, "write"));

        var latest = (await DeckSeed.ActivityAsync(Client, deck))[0];
        latest.GetProperty("summary").GetString().ShouldBe($"changed sharing: Named people (+{alex} write, {mira} read → write)");
    }

    [Test]
    public async Task Put_WhenAccessChanged_ShouldRecordActivityAndNotNotify()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var mira = Fresh("mira");
        await PutPeopleAsync(deck, "named", (mira, "read"));
        var before = NOTIFIER.To(mira).Count;
        var recipientsBefore = GUARD.CallsWith(mira).Count;

        var view = await PutPeopleAsync(deck, "named", (mira, "write"));

        AccessOf(view).ShouldBe([(mira, "write")]);
        (await DeckSeed.ActivityAsync(Client, deck))[0].GetProperty("summary").GetString().ShouldBe($"changed sharing: Named people ({mira} read → write)");
        NOTIFIER.To(mira).Count.ShouldBe(before);
        GUARD.CallsWith(mira).Count.ShouldBe(recipientsBefore);
        FakeMail.SentTo(mira).ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenPersonHasAccount_ShouldNameThemWithAvatar()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            await TestPeople.SignInAsync(scope.ServiceProvider, TestUser.Alex);
        }
        var deck = await SharingApi.CreateDeckAsync(Client);
        await PutPeopleAsync(deck, "named", (TestUser.Alex.Email.ToUpperInvariant(), "write"));

        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        var person = view.GetProperty("people").EnumerateArray().Single();
        person.GetProperty("name").GetString().ShouldBe(TestUser.Alex.Name);
        person.GetProperty("avatar").GetProperty("initials").GetString().ShouldNotBeNullOrEmpty();
        person.GetProperty("access").GetString().ShouldBe("write");
    }

    [Test]
    public async Task Get_WhenNoAccount_ShouldHaveNullName()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var nobody = Fresh("nobody");
        await PutPeopleAsync(deck, "named", (nobody, "read"));

        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");

        var person = view.GetProperty("people").EnumerateArray().Single();
        person.GetProperty("email").GetString().ShouldBe(nobody);
        person.GetProperty("name").ValueKind.ShouldBe(JsonValueKind.Null);
        person.GetProperty("avatar").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Put_WhenPeopleChanged_ShouldKeepOwnerAndLogOnlyAdded()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var alex = Fresh("alex");
        var mira = Fresh("mira");
        await PutPeopleAsync(deck, "named", (mira, "read"));

        var view = await PutPeopleAsync(deck, "named", (alex, "read"), (mira, "write"));

        var owner = view.GetProperty("owner");
        owner.GetProperty("email").GetString().ShouldBe(TestUser.Sam.Email);
        owner.GetProperty("name").GetString().ShouldBe(TestUser.Sam.Name);
        owner.GetProperty("avatar").GetProperty("initials").GetString().ShouldBe("SR");
        GUARD.CallsWith(alex).Count.ShouldBe(1);
        GUARD.CallsWith(mira).Count.ShouldBe(1);
        NOTIFIER.To(alex).Count.ShouldBe(1);
        NOTIFIER.To(mira).Count.ShouldBe(1);
        using var scope = Factory.Services.CreateScope();
        var sam = await TestPeople.SignInAsync(scope.ServiceProvider, TestUser.Sam);
        var logged = (await scope.ServiceProvider.GetRequiredService<IShareRecipients>().DistinctSinceAsync(sam.Id, DateTimeOffset.MinValue)).Select(r => r.Email).ToList();
        logged.ShouldContain(alex);
        logged.Count(email => email == mira).ShouldBe(1);
    }

    [Test]
    public async Task Put_WhenPendingSharer_ShouldReturn403AndStoreNothing()
    {
        var deck = await SharingApi.CreateDeckAsync(Client);
        var target = Fresh("target");
        using var pending = ClientFor(TestUser.NewPending());

        var response = await SharingApi.PutAsync(pending, deck, new { level = "named", people = new[] { new { email = target, access = "write" } } });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var view = await Client.GetFromJsonAsync<JsonElement>($"/api/decks/{deck}/sharing");
        view.GetProperty("people").GetArrayLength().ShouldBe(0);
        GUARD.CallsWith(target).ShouldBeEmpty();
    }

    private FakeMailSender FakeMail => Factory.Services.GetRequiredService<FakeMailSender>();

    private static string Fresh(string name) => $"{name}-{Guid.NewGuid().ToString("N")[..8]}@team.example";

    private async Task<JsonElement> PutPeopleAsync(string deck, string level, params (string Email, string Access)[] people)
    {
        var response = await SharingApi.PutAsync(Client, deck, new { level, people = people.Select(p => new { email = p.Email, access = p.Access }).ToArray() });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static IReadOnlyList<(string Email, string Access)> AccessOf(JsonElement view)
        => view.GetProperty("people").EnumerateArray().Select(p => (p.GetProperty("email").GetString()!, p.GetProperty("access").GetString()!)).ToList();

    private sealed class RecordingGuard : IShareGuard
    {
        private readonly ConcurrentQueue<IReadOnlyList<string>> _calls = new();

        public IReadOnlyList<IReadOnlyList<string>> CallsWith(string email) => [.. _calls.Where(call => call.Contains(ViewerEmails.Normalize(email)))];

        public Task AdmitAsync(Guid sharerId, IReadOnlyList<string> newlyAdded)
        {
            _calls.Enqueue(newlyAdded);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotifier : IShareNotifier
    {
        private readonly ConcurrentQueue<ShareNotice> _notices = new();

        public IReadOnlyList<ShareNotice> To(string email) => [.. _notices.Where(notice => notice.Added.Contains(ViewerEmails.Normalize(email)))];

        public Task QueueAsync(ShareNotice notice)
        {
            _notices.Enqueue(notice);
            return Task.CompletedTask;
        }
    }
}
