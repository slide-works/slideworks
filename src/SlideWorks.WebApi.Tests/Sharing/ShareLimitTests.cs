using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.Core;
using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// The new-people limits on sharing (D10, D13, D33): 10 an hour and 30 a day per sharer, counted as distinct emails the sharer
// newly added to any deck, with the ones being added now. Each test is its own approved sharer, so counts never meet.
[TestFixture]
public class ShareLimitTests : ApiTestFixture
{
    private readonly FakeMailSender _mail = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services
        .AddSingleton(_mail).AddSingleton<SlideWorks.WebApi.Infrastructure.Mail.IMailSender>(_mail);

    [Test]
    public async Task Put_WhenEleventhNewPersonInAnHour_ShouldReturn429AndStoreNothing()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var deck = await SharingApi.CreateDeckAsync(sharer);
        var ten = ShareLimitApi.NewEmails(10);
        (await ShareLimitApi.ShareAsync(sharer, deck, ten)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var eleventh = ShareLimitApi.NewEmails(1)[0];
        var activityBefore = (await DeckSeed.ActivityAsync(sharer, deck)).Count;

        var refused = await ShareLimitApi.ShareAsync(sharer, deck, [.. ten, eleventh]);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await ShareLimitApi.ErrorAsync(refused)).ShouldStartWith("You can share with 10 new people an hour. Try again at ");
        refused.Headers.RetryAfter!.Delta!.Value.ShouldBe(TimeSpan.FromSeconds(3601));
        (await ShareLimitApi.ViewersAsync(sharer, deck)).ShouldBe(ten.Order());
        (await DeckSeed.ActivityAsync(sharer, deck)).Count.ShouldBe(activityBefore);
        _mail.SentTo(eleventh).ShouldBeEmpty();
        Clock.Advance(TimeSpan.FromMinutes(61));
        var fresh = ShareLimitApi.NewEmails(9);
        (await ShareLimitApi.ShareAsync(sharer, deck, [.. fresh, eleventh])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ShareLimitApi.ShareAsync(sharer, deck, [.. fresh, eleventh, ShareLimitApi.NewEmails(1)[0]])).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task Put_WhenBatchAloneOverTheLimit_ShouldRefuseWithoutResetTime()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var deck = await SharingApi.CreateDeckAsync(sharer);

        var refused = await ShareLimitApi.ShareAsync(sharer, deck, ShareLimitApi.NewEmails(11));

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await ShareLimitApi.ErrorAsync(refused)).ShouldBe("You can share with 10 new people an hour. This share adds 11.");
        refused.Headers.RetryAfter.ShouldBeNull();
    }

    [Test]
    public async Task SetSharing_WhenOverLimit_ShouldReturnToolErrorWithResetTime()
    {
        var user = TestUser.NewApproved();
        using var sharer = ClientFor(user);
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, user, "Claude");
        var deck = await DeckSeed.CreateDeckAsync(agent);
        var viaHttp = ShareLimitApi.NewEmails(6);
        var viaMcp = ShareLimitApi.NewEmails(4);
        (await ShareLimitApi.ShareAsync(sharer, deck, viaHttp)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var shared = await agent.CallToolAsync("set_sharing", new { deck, level = "named", viewers = viaHttp.Concat(viaMcp).ToArray() });
        shared.IsError.ShouldBeFalse(shared.Json.ToString());

        var over = await agent.CallToolAsync("set_sharing", new { deck, level = "named", viewers = viaHttp.Concat(viaMcp).Append(ShareLimitApi.NewEmails(1)[0]).ToArray() });
        var viaHttpAgain = await ShareLimitApi.ShareAsync(sharer, deck, [.. viaHttp, .. viaMcp, ShareLimitApi.NewEmails(1)[0]]);

        over.IsError.ShouldBeTrue();
        over.Json.GetString()!.ShouldStartWith("You can share with 10 new people an hour. Try again at ");
        viaHttpAgain.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await ShareLimitApi.ViewersAsync(sharer, deck)).Length.ShouldBe(10);
    }

    [Test]
    public async Task Put_WhenSamePersonAddedToFiveDecks_ShouldCountOnce()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var person = ShareLimitApi.NewEmails(1)[0];
        for (var i = 0; i < 5; i++)
        {
            var deck = await SharingApi.CreateDeckAsync(sharer);
            (await ShareLimitApi.ShareAsync(sharer, deck, person)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        var last = await SharingApi.CreateDeckAsync(sharer);

        var nine = await ShareLimitApi.ShareAsync(sharer, last, ShareLimitApi.NewEmails(9));
        var tenth = await ShareLimitApi.ShareAsync(sharer, last, [.. await ShareLimitApi.ViewersAsync(sharer, last), ShareLimitApi.NewEmails(1)[0]]);

        nine.StatusCode.ShouldBe(HttpStatusCode.OK);
        tenth.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task Put_WhenRemovedAndAddedAgainInWindow_ShouldCountOnce()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var deck = await SharingApi.CreateDeckAsync(sharer);
        var ten = ShareLimitApi.NewEmails(10);
        (await ShareLimitApi.ShareAsync(sharer, deck, ten)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ShareLimitApi.ShareAsync(sharer, deck, ten.Skip(1).ToArray())).StatusCode.ShouldBe(HttpStatusCode.OK);

        var again = await ShareLimitApi.ShareAsync(sharer, deck, ten);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Put_WhenAddedAgainAfterWindow_ShouldCountAgain()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var deck = await SharingApi.CreateDeckAsync(sharer);
        var person = ShareLimitApi.NewEmails(1)[0];
        (await ShareLimitApi.ShareAsync(sharer, deck, person)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ShareLimitApi.ShareAsync(sharer, deck)).StatusCode.ShouldBe(HttpStatusCode.OK);
        Clock.Advance(TimeSpan.FromMinutes(61));
        var nine = ShareLimitApi.NewEmails(9);
        (await ShareLimitApi.ShareAsync(sharer, deck, nine)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var back = await ShareLimitApi.ShareAsync(sharer, deck, [.. nine, person]);
        var over = await ShareLimitApi.ShareAsync(sharer, deck, [.. nine, person, ShareLimitApi.NewEmails(1)[0]]);

        back.StatusCode.ShouldBe(HttpStatusCode.OK);
        over.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task Put_WhenMixedBatchOverLimit_ShouldResetWhenUnionFits()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var first = await SharingApi.CreateDeckAsync(sharer);
        var second = await SharingApi.CreateDeckAsync(sharer);
        var counted = ShareLimitApi.NewEmails(1)[0];
        var start = Clock.GetUtcNow();
        (await ShareLimitApi.ShareAsync(sharer, first, counted)).StatusCode.ShouldBe(HttpStatusCode.OK);
        Clock.Advance(TimeSpan.FromMinutes(10));
        (await ShareLimitApi.ShareAsync(sharer, first, [counted, .. ShareLimitApi.NewEmails(9)])).StatusCode.ShouldBe(HttpStatusCode.OK);
        Clock.Advance(TimeSpan.FromMinutes(1));
        var batch = new[] { counted, ShareLimitApi.NewEmails(1)[0] };

        var refused = await ShareLimitApi.ShareAsync(sharer, second, batch);

        // The batch holds the oldest entry, so its expiry (start + 1 h) frees nothing: the next-oldest, added 10 minutes in, does.
        var resetAt = start.AddMinutes(10).AddHours(1).AddSeconds(1);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter!.Delta!.Value.ShouldBe(resetAt - Clock.GetUtcNow());
        (await ShareLimitApi.ErrorAsync(refused)).ShouldBe($"You can share with 10 new people an hour. Try again at {DisplayDate.Format(resetAt)}.");
        Clock.Advance(resetAt - Clock.GetUtcNow());
        (await ShareLimitApi.ShareAsync(sharer, second, batch)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Put_WhenPersonAlreadyOnDeck_ShouldNotCount()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var deck = await SharingApi.CreateDeckAsync(sharer);
        var ten = ShareLimitApi.NewEmails(10);
        (await ShareLimitApi.ShareAsync(sharer, deck, ten)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var sameList = await SharingApi.PutAsync(sharer, deck, new { level = "link", viewers = ten });
        var plusOne = await ShareLimitApi.ShareAsync(sharer, deck, [.. ten, ShareLimitApi.NewEmails(1)[0]]);

        sameList.StatusCode.ShouldBe(HttpStatusCode.OK);
        plusOne.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task Put_WhenChangingAccessOrRemoving_ShouldNotCount()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var deck = await SharingApi.CreateDeckAsync(sharer);
        var five = ShareLimitApi.NewEmails(5);
        (await ShareLimitApi.ShareWithAccessAsync(sharer, deck, "read", five)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ShareLimitApi.ShareWithAccessAsync(sharer, deck, "write", five)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var kept = five.Take(3).ToArray();
        (await ShareLimitApi.ShareWithAccessAsync(sharer, deck, "read", kept)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var fiveMore = await ShareLimitApi.ShareWithAccessAsync(sharer, deck, "read", [.. kept, .. ShareLimitApi.NewEmails(5)]);
        var sixth = await ShareLimitApi.ShareWithAccessAsync(sharer, deck, "read", [.. kept, .. ShareLimitApi.NewEmails(6)]);

        fiveMore.StatusCode.ShouldBe(HttpStatusCode.OK);
        sixth.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Test]
    public async Task Put_WhenHourPassed_ShouldAllowAgain()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var deck = await SharingApi.CreateDeckAsync(sharer);
        var ten = ShareLimitApi.NewEmails(10);
        (await ShareLimitApi.ShareAsync(sharer, deck, ten)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var next = ShareLimitApi.NewEmails(1)[0];
        (await ShareLimitApi.ShareAsync(sharer, deck, [.. ten, next])).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        Clock.Advance(TimeSpan.FromMinutes(61));
        var allowed = await ShareLimitApi.ShareAsync(sharer, deck, [.. ten, next]);

        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Put_WhenThirtyOneNewPeopleInADay_ShouldRefuseWithDayMessage()
    {
        using var sharer = ClientFor(TestUser.NewApproved());
        var deck = await SharingApi.CreateDeckAsync(sharer);
        var start = Clock.GetUtcNow();
        var shared = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            shared.AddRange(ShareLimitApi.NewEmails(10));
            (await ShareLimitApi.ShareAsync(sharer, deck, [.. shared])).StatusCode.ShouldBe(HttpStatusCode.OK);
            Clock.Advance(TimeSpan.FromMinutes(61));
        }

        var refused = await ShareLimitApi.ShareAsync(sharer, deck, [.. shared, ShareLimitApi.NewEmails(1)[0]]);

        var resetAt = start.AddDays(1).AddSeconds(1);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await ShareLimitApi.ErrorAsync(refused)).ShouldBe($"You can share with 30 new people a day. Try again at {DisplayDate.Format(resetAt)}.");
        refused.Headers.RetryAfter!.Delta!.Value.ShouldBe(resetAt - Clock.GetUtcNow());
    }
}
