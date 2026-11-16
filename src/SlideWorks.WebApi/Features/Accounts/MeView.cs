namespace SlideWorks.WebApi.Features.Accounts;

// GET /api/me: the signed-in person, for the shell's user menu (src/contracts/screens/me.json). Access is "pending" or
// "approved"; a rejected person never reaches this. PendingRequests is 0 for a non-admin. AdminMissing is true while no
// admin exists.
public sealed record MeView(
    string Email,
    string Name,
    string? PictureUrl,
    string Access,
    bool IsAdmin,
    int PendingRequests,
    bool AdminMissing);
