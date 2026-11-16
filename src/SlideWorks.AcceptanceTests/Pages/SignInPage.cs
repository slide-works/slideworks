namespace SlideWorks.AcceptanceTests.Pages;

// /signin, where the auth gate sends anyone who isn't signed in.
public sealed class SignInPage(IPage page)
{
    public ILocator Root => page.GetByTestId("signin-page");
}
