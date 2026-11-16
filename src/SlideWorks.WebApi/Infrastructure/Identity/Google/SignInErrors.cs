namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// The error codes sign-in puts on /signin?error=. The sign-in page turns each into a message.
public static class SignInErrors
{
    // The person cancelled on Google's consent screen.
    public const string CANCELLED = "cancelled";

    // A stale or forged state, a failed code exchange, or Google unreachable.
    public const string FAILED = "failed";

    // Google didn't say the email is verified.
    public const string UNVERIFIED = "unverified";

    // The email already belongs to a person who signs in with another Google account.
    public const string EMAIL_LINKED = "email_linked";

    // /signin?error={error}&returnTo={returnTo}, so trying again still comes back to where the person was going.
    public static string SignInPath(string error, string returnTo)
        => $"/signin?error={Uri.EscapeDataString(error)}&returnTo={Uri.EscapeDataString(returnTo)}";
}
