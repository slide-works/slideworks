using System.Text.RegularExpressions;
using SlideWorks.AcceptanceTests.Pages;
using SlideWorks.WebApi.Tests.Support;

// Journeys are named for the story they tell, not Subject_When_Should, so AG0005 is off for this file.
#pragma warning disable AG0005

namespace SlideWorks.AcceptanceTests.Journeys;

// The sign-up approval loop in two browsers: the person (this test's own Page) and Ada, the admin, in a second browser
// context. Everything goes through pages. Each journey makes its own NewPending() person, because the journeys share one
// host and database. The shell's badge is loaded once per page load, so Ada reloads before she reads it again.
public partial class SignUpApprovalJourneys : AcceptanceTest
{
    [Test]
    public async Task PendingPersonRequestsAccessAndAdminApproves()
    {
        var person = TestUser.NewPending();
        await SignIn.AsAsync(Page, person);
        var pending = new PendingPage(Page);
        await new DecksPage(Page).GotoAsync();
        await Expect(Page).ToHaveURLAsync(PendingUrl());
        await Expect(pending.Heading).ToHaveTextAsync("Waiting for approval");
        await Expect(pending.Email).ToHaveTextAsync(person.Email);

        await using var adminSession = await SignInAdminAsync();
        var admin = adminSession.Users;
        await admin.GotoAsync();
        var row = admin.Row(person.Email);
        await Expect(row).ToHaveCountAsync(1);
        await Expect(admin.Badge).ToBeVisibleAsync();
        var before = int.Parse(await admin.Badge.InnerTextAsync());

        await AdminUsersPage.Approve(row).ClickAsync();
        await Expect(AdminUsersPage.Approve(row)).ToHaveCountAsync(0);

        // The shell's count comes from /api/me, loaded once per page load: reload, then it has dropped.
        await admin.GotoAsync();
        if (before == 1)
        {
            await Expect(admin.Badge).ToBeHiddenAsync();
        }
        else
        {
            await Expect(admin.Badge).ToHaveTextAsync((before - 1).ToString());
        }

        await Page.ReloadAsync();
        await Expect(Page).ToHaveURLAsync(DecksUrl());
        await Expect(new DecksPage(Page).Library).ToBeVisibleAsync();
    }

    [Test]
    public async Task AdminRevokesPersonAndTheirSessionEnds()
    {
        var person = TestUser.NewPending();
        await SignIn.AsAsync(Page, person);
        var decks = new DecksPage(Page);
        await decks.GotoAsync();
        await Expect(Page).ToHaveURLAsync(PendingUrl());

        await using var adminSession = await SignInAdminAsync();
        var admin = adminSession.Users;
        await admin.GotoAsync();
        var row = admin.Row(person.Email);
        await Expect(row).ToHaveCountAsync(1);
        await AdminUsersPage.Approve(row).ClickAsync();
        await Expect(AdminUsersPage.Approve(row)).ToHaveCountAsync(0);

        await decks.GotoAsync();
        await Expect(decks.Library).ToBeVisibleAsync();

        await AdminUsersPage.Revoke(row).ClickAsync();
        await AdminUsersPage.ConfirmRevoke(row).ClickAsync();
        await Expect(AdminUsersPage.Revoke(row)).ToHaveCountAsync(0);

        await decks.GotoAsync();
        await new SignInPage(Page).ShouldBeShownWithReturnToAsync(DecksPage.PATH);
        await Expect(decks.Shell).ToHaveCountAsync(0);
    }

    [Test]
    public async Task RejectedPersonCannotGetBackIn()
    {
        var person = TestUser.NewPending();
        await SignIn.AsAsync(Page, person);
        var decks = new DecksPage(Page);
        await decks.GotoAsync();
        await Expect(Page).ToHaveURLAsync(PendingUrl());

        await using var adminSession = await SignInAdminAsync();
        var admin = adminSession.Users;
        await admin.GotoAsync();
        var row = admin.Row(person.Email);
        await Expect(row).ToHaveCountAsync(1);
        await AdminUsersPage.Reject(row).ClickAsync();
        await Expect(AdminUsersPage.Reject(row)).ToHaveCountAsync(0);

        await decks.GotoAsync();
        await new SignInPage(Page).ShouldBeShownWithReturnToAsync(DecksPage.PATH);

        // Signing in again with the test scheme changes nothing: they stay signed out.
        await SignIn.AsAsync(Page, person);
        await decks.GotoAsync();
        await new SignInPage(Page).ShouldBeShownWithReturnToAsync(DecksPage.PATH);
        await Expect(decks.Shell).ToHaveCountAsync(0);
    }

    // Ada in her own browser context, so her cookie never mixes with the person's.
    private async Task<AdminSession> SignInAdminAsync()
    {
        var context = await Browser.NewContextAsync(ContextOptions());
        var page = await context.NewPageAsync();
        await SignIn.AsAsync(page, TestUser.Ada);
        return new AdminSession(context, new AdminUsersPage(page));
    }

    private sealed class AdminSession(IBrowserContext context, AdminUsersPage users) : IAsyncDisposable
    {
        public AdminUsersPage Users { get; } = users;

        public ValueTask DisposeAsync() => context.DisposeAsync();
    }

    [GeneratedRegex(@"/pending$")]
    private static partial Regex PendingUrl();

    [GeneratedRegex(@"/decks$")]
    private static partial Regex DecksUrl();
}
