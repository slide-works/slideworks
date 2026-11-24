namespace SlideWorks.AcceptanceTests.Pages;

// The signed-in person's menu in the shell's top bar: the initials button, the panel it opens (who is signed in) and
// its Sign out button.
public sealed class UserMenu(IPage page)
{
    public ILocator Button => page.GetByTestId("user-menu-button");

    // The avatar inside the button: the person's picture, or their initials on their colour (data-colour).
    public ILocator Avatar => Button.GetByTestId("user-menu-avatar");

    // The uploaded or Google picture inside the avatar; absent while initials show.
    public ILocator AvatarPicture => Button.GetByTestId("user-menu-avatar-picture");

    public ILocator AvatarInitials => Button.GetByTestId("user-menu-avatar-initials");

    public ILocator Panel => page.GetByTestId("user-menu-panel");

    public ILocator SignOutButton => Panel.GetByTestId("user-menu-signout");

    public ILocator Error => Panel.GetByTestId("user-menu-error");

    public Task OpenAsync() => Button.ClickAsync();

    public Task SignOutAsync() => SignOutButton.ClickAsync();
}
