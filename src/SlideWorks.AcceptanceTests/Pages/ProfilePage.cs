namespace SlideWorks.AcceptanceTests.Pages;

// The profile page at /profile, inside the signed-in shell: Identity, Account and Connected agents panels, and the user
// menu that leads to it. The controls are read-only until the profile tasks of wave 1 add them; their locators are here
// so those tasks and the journey share one place.
public sealed class ProfilePage(IPage page)
{
    public const string PATH = "/profile";

    public ILocator Page => page.GetByTestId("profile-page");

    public ILocator Error => page.GetByTestId("profile-error");

    public ILocator IdentityPanel => page.GetByTestId("profile-identity-panel");

    public ILocator AccountPanel => page.GetByTestId("profile-account-panel");

    public ILocator AgentsPanel => page.GetByTestId("profile-agents");

    public ILocator Name => page.GetByTestId("profile-name");

    public ILocator Email => page.GetByTestId("profile-email");

    public ILocator Avatar => page.GetByTestId("profile-avatar");

    // Identity controls added in wave 1.
    public ILocator NameInput => page.GetByTestId("profile-name-input");

    public ILocator ColourOption(string colour) => page.GetByTestId($"profile-colour-{colour}");

    public ILocator Save => page.GetByTestId("profile-save");

    public ILocator PhotoInput => page.GetByTestId("profile-photo-input");

    public ILocator RemovePhoto => page.GetByTestId("profile-photo-remove");

    // Says which picture is shown: the person's photo, their Google picture or initials.
    public ILocator PhotoSource => page.GetByTestId("profile-photo-source");

    public ILocator TimeZone => page.GetByTestId("profile-time-zone");

    public ILocator TimeZoneSelect => page.GetByTestId("profile-time-zone-select");

    public ILocator Access => page.GetByTestId("profile-access");

    public ILocator AdminLink => page.GetByTestId("profile-admin-link");

    public ILocator AgentRows => page.GetByTestId("profile-agent");

    public static ILocator AgentName(ILocator row) => row.GetByTestId("profile-agent-name");

    public static ILocator AgentConnected(ILocator row) => row.GetByTestId("profile-agent-connected");

    public static ILocator AgentLastUsed(ILocator row) => row.GetByTestId("profile-agent-last-used");

    // Agent controls added in wave 1: Disconnect asks "Disconnect Claude?" first, with Keep and Disconnect.
    public static ILocator Disconnect(ILocator row) => row.GetByTestId("profile-agent-disconnect");

    public static ILocator ConfirmDisconnect(ILocator row) => row.GetByTestId("profile-agent-confirm-disconnect");

    public static ILocator KeepAgent(ILocator row) => row.GetByTestId("profile-agent-keep");

    // The user menu, opened from the top bar.
    public ILocator MenuButton => page.GetByTestId("user-menu-button");

    public ILocator MenuProfile => page.GetByTestId("user-menu-profile");

    public ILocator MenuPeople => page.GetByTestId("user-menu-people");

    public ILocator MenuSignOut => page.GetByTestId("user-menu-signout");
}
