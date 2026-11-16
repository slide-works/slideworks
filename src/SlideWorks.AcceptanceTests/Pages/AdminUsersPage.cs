using System.Text.RegularExpressions;

namespace SlideWorks.AcceptanceTests.Pages;

// The admin's people list at /admin/users, inside the signed-in shell, and the shell's "Admin" link with its count of
// waiting requests.
public sealed class AdminUsersPage(IPage page)
{
    public const string PATH = "/admin/users";

    public ILocator Page => page.GetByTestId("admin-users-page");

    public ILocator Rows => page.GetByTestId("admin-user-row");

    public ILocator Error => page.GetByTestId("admin-users-error");

    public ILocator PendingCount => page.GetByTestId("admin-pending-count");

    public ILocator AdminLink => page.GetByTestId("admin-link");

    // The shell's count of waiting requests; absent while nobody is waiting.
    public ILocator Badge => page.GetByTestId("admin-link-badge");

    // One person, by their exact email.
    public ILocator Row(string email) =>
        Rows.Filter(new LocatorFilterOptions
        {
            Has = page.GetByTestId("admin-user-email").Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{Regex.Escape(email)}$") }),
        });

    public static ILocator Access(ILocator row) => row.GetByTestId("admin-user-access");

    public static ILocator RowError(ILocator row) => row.GetByTestId("admin-user-error");

    public static ILocator Approve(ILocator row) => row.GetByTestId("admin-approve");

    public static ILocator Reject(ILocator row) => row.GetByTestId("admin-reject");

    public static ILocator MakeAdmin(ILocator row) => row.GetByTestId("admin-make-admin");

    public static ILocator RemoveAdmin(ILocator row) => row.GetByTestId("admin-remove-admin");

    public static ILocator Revoke(ILocator row) => row.GetByTestId("admin-revoke");

    public static ILocator ConfirmRevoke(ILocator row) => row.GetByTestId("admin-revoke-confirm");

    public static ILocator CancelRevoke(ILocator row) => row.GetByTestId("admin-revoke-cancel");

    public async Task GotoAsync() => await page.GotoAsync(PATH);
}
