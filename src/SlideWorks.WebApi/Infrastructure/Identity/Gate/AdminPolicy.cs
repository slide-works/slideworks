namespace SlideWorks.WebApi.Infrastructure.Identity.Gate;

// An approved person who is an admin (S3). A non-admin gets 403 "Admins only", and a pending person pending_approval.
// AuthSetup registers it.
public static class AdminPolicy
{
    public const string NAME = "Admin";
}
