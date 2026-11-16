using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Identity.Approval;

// IUserAccounts (S1) through the app's own container: every allowed change, every refused one, the list order, the
// stamps and the last-admin guard. Each test acts on fresh NewPending() people. Ada is the admin (SLIDEWORKS_ADMIN_EMAIL),
// and every test leaves her the only one, so the last-admin tests see exactly one admin.
[TestFixture]
public class UserAccountsTests : ApiTestFixture
{
    public enum Start
    {
        Pending,
        Approved,
        Rejected,
        Admin,
    }

    public enum Change
    {
        Approve,
        Reject,
        Revoke,
        MakeAdmin,
        RemoveAdmin,
    }

    [TearDown]
    public async Task LeaveAdaTheOnlyAdmin()
    {
        var ada = await AdaAsync();
        var admins = (await UseAsync(a => a.ListAsync())).Where(u => u.IsAdmin).ToList();
        if (admins.TrueForAll(u => u.Id != ada.Id))
        {
            (await UseAsync(a => a.MakeAdminAsync(ada.Id, admins[0].Id))).ShouldBe(AccountChange.Changed);
        }
        foreach (var other in admins.Where(u => u.Id != ada.Id))
        {
            (await UseAsync(a => a.RemoveAdminAsync(other.Id, ada.Id))).ShouldBe(AccountChange.Changed);
        }
    }

    [TestCase(Change.Approve, Start.Pending, UserAccess.Approved, false)]
    [TestCase(Change.Approve, Start.Rejected, UserAccess.Approved, false)]
    [TestCase(Change.Reject, Start.Pending, UserAccess.Rejected, false)]
    [TestCase(Change.Revoke, Start.Approved, UserAccess.Rejected, false)]
    [TestCase(Change.Revoke, Start.Admin, UserAccess.Rejected, false)]
    [TestCase(Change.MakeAdmin, Start.Approved, UserAccess.Approved, true)]
    [TestCase(Change.RemoveAdmin, Start.Admin, UserAccess.Approved, false)]
    public async Task Change_WhenAllowed_ShouldChange(Change change, Start start, UserAccess access, bool isAdmin)
    {
        var person = await PersonAsync(start);

        var result = await ApplyAsync(change, person.Id);

        result.ShouldBe(AccountChange.Changed);
        var after = (await UseAsync(a => a.FindAsync(person.Id)))!;
        after.Access.ShouldBe(access);
        after.IsAdmin.ShouldBe(isAdmin);
    }

    [TestCase(Change.Approve, Start.Approved)]
    [TestCase(Change.Approve, Start.Admin)]
    [TestCase(Change.Reject, Start.Approved)]
    [TestCase(Change.Reject, Start.Admin)]
    [TestCase(Change.Reject, Start.Rejected)]
    [TestCase(Change.Revoke, Start.Pending)]
    [TestCase(Change.Revoke, Start.Rejected)]
    [TestCase(Change.MakeAdmin, Start.Pending)]
    [TestCase(Change.MakeAdmin, Start.Rejected)]
    [TestCase(Change.MakeAdmin, Start.Admin)]
    [TestCase(Change.RemoveAdmin, Start.Pending)]
    [TestCase(Change.RemoveAdmin, Start.Approved)]
    [TestCase(Change.RemoveAdmin, Start.Rejected)]
    public async Task Change_WhenNotFromThisState_ShouldReturnNotAllowed(Change change, Start start)
    {
        var person = await PersonAsync(start);
        var before = (await UseAsync(a => a.FindAsync(person.Id)))!;

        var result = await ApplyAsync(change, person.Id);

        result.ShouldBe(AccountChange.NotAllowed);
        var after = (await UseAsync(a => a.FindAsync(person.Id)))!;
        after.Access.ShouldBe(before.Access);
        after.IsAdmin.ShouldBe(before.IsAdmin);
        after.AccessChangedAt.ShouldBe(before.AccessChangedAt);
    }

    [TestCase(Change.Approve)]
    [TestCase(Change.Reject)]
    [TestCase(Change.Revoke)]
    [TestCase(Change.MakeAdmin)]
    [TestCase(Change.RemoveAdmin)]
    public async Task Change_WhenNoSuchUser_ShouldReturnNotFound(Change change)
    {
        var result = await ApplyAsync(change, Guid.NewGuid());

        result.ShouldBe(AccountChange.NotFound);
    }

    [Test]
    public async Task List_ShouldOrderPendingThenApprovedThenRejected()
    {
        var older = await PersonAsync(Start.Pending);
        Clock.Advance(TimeSpan.FromMinutes(1));
        var newer = await PersonAsync(Start.Pending);
        await PersonAsync(Start.Approved);
        await PersonAsync(Start.Approved);
        await PersonAsync(Start.Rejected);
        await PersonAsync(Start.Rejected);

        var users = await UseAsync(a => a.ListAsync());

        users.Select(u => u.Access).ShouldBe(users.Select(u => u.Access).Order());
        var pending = users.Where(u => u.Access == UserAccess.Pending).ToList();
        pending.Select(u => u.CreatedAt).ShouldBe(pending.Select(u => u.CreatedAt).Order());
        pending.FindIndex(u => u.Id == older.Id).ShouldBeLessThan(pending.FindIndex(u => u.Id == newer.Id));
        foreach (var access in new[] { UserAccess.Approved, UserAccess.Rejected })
        {
            var names = users.Where(u => u.Access == access).Select(u => u.Name).ToList();
            names.Count.ShouldBeGreaterThanOrEqualTo(2);
            names.ShouldBe(names.Order(StringComparer.Ordinal));
        }
    }

    [Test]
    public async Task Change_ShouldStampWhenAndWho()
    {
        var ada = await AdaAsync();
        var person = await PersonAsync(Start.Pending);
        Clock.Advance(TimeSpan.FromMinutes(3));

        (await UseAsync(a => a.ApproveAsync(person.Id, ada.Id))).ShouldBe(AccountChange.Changed);

        var after = (await UseAsync(a => a.FindAsync(person.Id)))!;
        after.AccessChangedAt.ShouldBe(Clock.GetUtcNow());
        after.AccessChangedBy.ShouldBe(ada.Id);
    }

    [Test]
    public async Task PendingCount_ShouldCountPendingPeople()
    {
        var before = await UseAsync(a => a.PendingCountAsync());

        await PersonAsync(Start.Pending);
        await PersonAsync(Start.Approved);

        (await UseAsync(a => a.PendingCountAsync())).ShouldBe(before + 1);
        (await UseAsync(a => a.AnyAdminAsync())).ShouldBeTrue();
    }

    [Test]
    public async Task RemoveAdmin_WhenLastAdmin_ShouldReturnLastAdmin()
    {
        var ada = await AdaAsync();

        var result = await UseAsync(a => a.RemoveAdminAsync(ada.Id, ada.Id));

        result.ShouldBe(AccountChange.LastAdmin);
        (await UseAsync(a => a.FindAsync(ada.Id)))!.IsAdmin.ShouldBeTrue();
    }

    [Test]
    public async Task Revoke_WhenLastAdmin_ShouldReturnLastAdmin()
    {
        var ada = await AdaAsync();

        var result = await UseAsync(a => a.RevokeAsync(ada.Id, ada.Id));

        result.ShouldBe(AccountChange.LastAdmin);
        var after = (await UseAsync(a => a.FindAsync(ada.Id)))!;
        after.Access.ShouldBe(UserAccess.Approved);
        after.IsAdmin.ShouldBeTrue();
    }

    // Ada and another admin each try to remove the other, 20 attempts at once, each on its own thread and scope as
    // parallel requests would be. Exactly one removal wins and one admin is left.
    [Test]
    public async Task RemoveAdmin_WhenTwoAdminsRemoveEachOtherAtOnce_ShouldLeaveOne()
    {
        const int attempts = 20;
        var ada = await AdaAsync();
        var other = await PersonAsync(Start.Admin);
        var scopes = Enumerable.Range(0, attempts).Select(_ => Factory.Services.CreateScope()).ToList();
        AccountChange[] results;
        try
        {
            using var start = new Barrier(attempts);
            var races = scopes.Select((scope, i) =>
            {
                var accounts = scope.ServiceProvider.GetRequiredService<IUserAccounts>();
                var (target, by) = i % 2 == 0 ? (other.Id, ada.Id) : (ada.Id, other.Id);
                return Task.Factory.StartNew(
                    () =>
                    {
                        start.SignalAndWait(TimeSpan.FromSeconds(30)).ShouldBeTrue("every attempt should reach the start line");
                        return accounts.RemoveAdminAsync(target, by);
                    },
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();
            }).ToList();
            results = await Task.WhenAll(races);
        }
        finally
        {
            scopes.ForEach(scope => scope.Dispose());
        }

        results.Count(r => r == AccountChange.Changed).ShouldBe(1);
        results.ShouldAllBe(r => r == AccountChange.Changed || r == AccountChange.LastAdmin || r == AccountChange.NotAllowed);
        (await UseAsync(a => a.ListAsync())).Count(u => u.IsAdmin).ShouldBe(1);
    }

    private async Task<User> AdaAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await TestPeople.SignInAsync(scope.ServiceProvider, TestUser.Ada);
    }

    // A fresh person in the given state, moved there through IUserAccounts by Ada.
    private async Task<User> PersonAsync(Start start)
    {
        var ada = await AdaAsync();
        User person;
        using (var scope = Factory.Services.CreateScope())
        {
            person = await TestPeople.SignInAsync(scope.ServiceProvider, TestUser.NewPending());
        }
        if (start == Start.Rejected)
        {
            (await UseAsync(a => a.RejectAsync(person.Id, ada.Id))).ShouldBe(AccountChange.Changed);
        }
        if (start is Start.Approved or Start.Admin)
        {
            (await UseAsync(a => a.ApproveAsync(person.Id, ada.Id))).ShouldBe(AccountChange.Changed);
        }
        if (start == Start.Admin)
        {
            (await UseAsync(a => a.MakeAdminAsync(person.Id, ada.Id))).ShouldBe(AccountChange.Changed);
        }
        return (await UseAsync(a => a.FindAsync(person.Id)))!;
    }

    private async Task<AccountChange> ApplyAsync(Change change, Guid id)
    {
        var ada = await AdaAsync();
        return change switch
        {
            Change.Approve => await UseAsync(a => a.ApproveAsync(id, ada.Id)),
            Change.Reject => await UseAsync(a => a.RejectAsync(id, ada.Id)),
            Change.Revoke => await UseAsync(a => a.RevokeAsync(id, ada.Id)),
            Change.MakeAdmin => await UseAsync(a => a.MakeAdminAsync(id, ada.Id)),
            Change.RemoveAdmin => await UseAsync(a => a.RemoveAdminAsync(id, ada.Id)),
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };
    }

    private async Task<T> UseAsync<T>(Func<IUserAccounts, Task<T>> call)
    {
        using var scope = Factory.Services.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<IUserAccounts>());
    }
}
