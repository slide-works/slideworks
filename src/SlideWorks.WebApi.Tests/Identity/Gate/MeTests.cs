using System.Net.Http.Json;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Gate;

// GET /api/me's real values (S5): the person's access, whether they are an admin, the waiting requests (an admin's
// count, 0 for anyone else) and whether the server has no admin yet. Ada is the admin through SLIDEWORKS_ADMIN_EMAIL.
[TestFixture]
public class MeTests : ApiTestFixture
{
    [Test]
    public async Task Me_WhenPending_ShouldReturnPendingAccess()
    {
        var person = TestUser.NewPending();
        using var pending = ClientFor(person);

        var me = await MeAsync(pending);

        me.Email.ShouldBe(person.Email);
        me.Access.ShouldBe("pending");
        me.IsAdmin.ShouldBeFalse();
        me.PendingRequests.ShouldBe(0);
    }

    [Test]
    public async Task Me_WhenAdminWithRequests_ShouldReturnPendingCount()
    {
        using var ada = ClientFor(TestUser.Ada);
        var before = await MeAsync(ada);
        foreach (var person in new[] { TestUser.NewPending(), TestUser.NewPending() })
        {
            using var pending = ClientFor(person);
            await MeAsync(pending);
        }

        var after = await MeAsync(ada);

        after.Access.ShouldBe("approved");
        after.IsAdmin.ShouldBeTrue();
        after.AdminMissing.ShouldBeFalse();
        after.PendingRequests.ShouldBe(before.PendingRequests + 2);
    }

    [Test]
    public async Task Me_WhenNotAdmin_ShouldReturnZeroRequests()
    {
        using var pending = ClientFor(TestUser.NewPending());
        await MeAsync(pending);

        var me = await MeAsync(Client);

        me.Access.ShouldBe("approved");
        me.IsAdmin.ShouldBeFalse();
        me.PendingRequests.ShouldBe(0);
    }

    private static async Task<MeDto> MeAsync(HttpClient client) => (await client.GetFromJsonAsync<MeDto>("/api/me"))!;

    // A server whose SLIDEWORKS_ADMIN_EMAIL is unset and that has no admin, on its own host: the bootstrap admin can't be
    // undone, so the outer fixture's server can't be used once Ada has signed in.
    [TestFixture]
    public class WithoutAdmin : ApiTestFixture
    {
        protected override Dictionary<string, string?> ExtraConfig => new() { [IUserDirectory.ADMIN_EMAIL_SETTING] = "" };

        [Test]
        public async Task Me_WhenNoAdmin_ShouldSayAdminMissing()
        {
            using var pending = ClientFor(TestUser.NewPending());
            using var ada = ClientFor(TestUser.Ada);
            await MeAsync(ada);

            var me = await MeAsync(pending);

            me.AdminMissing.ShouldBeTrue();
            me.Access.ShouldBe("pending");
        }
    }

    private sealed record MeDto(string Email, string Name, string? PictureUrl, string Access, bool IsAdmin, int PendingRequests, bool AdminMissing);
}
