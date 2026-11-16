using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Approval;

// The first admin from SLIDEWORKS_ADMIN_EMAIL (S1, D3), through IUserDirectory.UpsertAsync, which every sign-in calls.
// The bootstrap only acts while no admin exists, and the first admin can't be undone, so each test starts its own host
// on its own database instead of sharing a fixture's.
[TestFixture]
public class AdminBootstrapTests
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    [Test]
    public async Task SignIn_WhenNoAdminAndEmailMatches_ShouldCreateApprovedAdmin()
    {
        using var host = new Host(TestUser.Ada.Email, _clock);

        var user = await host.SignInAsync(TestUser.Ada);

        user.Access.ShouldBe(UserAccess.Approved);
        user.IsAdmin.ShouldBeTrue();
        user.AccessChangedAt.ShouldBe(_clock.GetUtcNow());
        user.AccessChangedBy.ShouldBeNull();
        (await host.FindAsync(user.Id))!.IsAdmin.ShouldBeTrue();
    }

    [TestCase("ADA@Team.Example")]
    [TestCase("  ada@team.example \t")]
    public async Task SignIn_WhenEmailMatchesInOtherCaseOrWithSpaces_ShouldCreateApprovedAdmin(string adminEmail)
    {
        using var host = new Host(adminEmail, _clock);

        var user = await host.SignInAsync(TestUser.Ada);

        user.Access.ShouldBe(UserAccess.Approved);
        user.IsAdmin.ShouldBeTrue();
    }

    [Test]
    public async Task SignIn_WhenExistingPendingUserMatches_ShouldPromote()
    {
        using var host = new Host(adminEmail: null, _clock);
        var before = await host.SignInAsync(TestUser.Ada);
        host.SetAdminEmail(TestUser.Ada.Email);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var after = await host.SignInAsync(TestUser.Ada);

        before.Access.ShouldBe(UserAccess.Pending);
        before.IsAdmin.ShouldBeFalse();
        after.Id.ShouldBe(before.Id);
        after.Access.ShouldBe(UserAccess.Approved);
        after.IsAdmin.ShouldBeTrue();
        after.CreatedAt.ShouldBe(before.CreatedAt);
        after.AccessChangedAt.ShouldBe(_clock.GetUtcNow());
    }

    [Test]
    public async Task SignIn_WhenAdminExists_ShouldCreatePendingEvenIfEmailMatches()
    {
        using var host = new Host(TestUser.Ada.Email, _clock);
        (await host.SignInAsync(TestUser.Ada)).IsAdmin.ShouldBeTrue();
        var later = TestUser.NewPending();
        host.SetAdminEmail(later.Email);

        var user = await host.SignInAsync(later);

        user.Access.ShouldBe(UserAccess.Pending);
        user.IsAdmin.ShouldBeFalse();
        (await host.FindAsync(user.Id))!.Access.ShouldBe(UserAccess.Pending);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task SignIn_WhenVariableUnsetOrEmpty_ShouldCreatePending(string? adminEmail)
    {
        using var host = new Host(adminEmail, _clock);

        var user = await host.SignInAsync(TestUser.Ada);

        user.Access.ShouldBe(UserAccess.Pending);
        user.IsAdmin.ShouldBeFalse();
        user.AccessChangedAt.ShouldBeNull();
    }

    // A test host on its own database, with SLIDEWORKS_ADMIN_EMAIL as given (null means unset).
    private sealed class Host : IDisposable
    {
        private readonly string _dbPath = TestAppFactory.NewDatabasePath();
        private readonly WebApplicationFactory<ApiMarker> _factory;

        public Host(string? adminEmail, FakeTimeProvider clock)
        {
            _factory = TestAppFactory.Create(_dbPath, clock, new Dictionary<string, string?> { [IUserDirectory.ADMIN_EMAIL_SETTING] = adminEmail });
        }

        // The setting is read on every upsert, so a running server picks up a change.
        public void SetAdminEmail(string email) => _factory.Services.GetRequiredService<IConfiguration>()[IUserDirectory.ADMIN_EMAIL_SETTING] = email;

        // What a sign-in stores, as the upsert returns it.
        public async Task<User> SignInAsync(TestUser person)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IUserDirectory>().UpsertAsync(person.GoogleSubject, person.Email, person.Name, pictureUrl: null);
        }

        public async Task<User?> FindAsync(Guid id)
        {
            using var scope = _factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IUserAccounts>().FindAsync(id);
        }

        public void Dispose()
        {
            _factory.Dispose();
            TestAppFactory.DeleteDatabase(_dbPath);
        }
    }
}
