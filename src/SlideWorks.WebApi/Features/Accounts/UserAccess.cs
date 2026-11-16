namespace SlideWorks.WebApi.Features.Accounts;

// Whether a person may use SlideWorks. A first sign-in creates them Pending, an admin approves or rejects them, and
// revoking an approved person rejects them again. Rejected people get no session.
public enum UserAccess
{
    Pending,
    Approved,
    Rejected,
}
