namespace SlideWorks.WebApi.Infrastructure.Identity.Gate;

// Any signed-in person, whatever their access (S3): GET /api/me and POST /api/signout, so a pending person can see who
// they are and sign out. AuthSetup registers it.
public static class SignedInPolicy
{
    public const string NAME = "SignedIn";
}
