namespace SlideWorks.AcceptanceTests.Pages;

// /pending, where a signed-in person waits for an admin to approve them: who they are signed in as, the
// no-admin note (only when the server has no admin) and the Sign out button.
public sealed class PendingPage(IPage page)
{
    public const string PATH = "/pending";

    public ILocator Root => page.GetByTestId("pending-page");

    public ILocator Heading => page.GetByTestId("pending-heading");

    public ILocator Email => page.GetByTestId("pending-email");

    public ILocator AdminMissingNote => page.GetByTestId("pending-admin-missing");

    public ILocator SignOutButton => page.GetByTestId("pending-sign-out");

    public ILocator SignOutError => page.GetByTestId("pending-sign-out-error");

    public Task GotoAsync() => page.GotoAsync(PATH);

    public Task SignOutAsync() => SignOutButton.ClickAsync();
}
