using SlideWorks.WebApi.Features.Accounts.Avatars;

namespace SlideWorks.WebApi.Features.Accounts;

// GET /api/me: the signed-in person, for the shell's user menu (src/contracts/screens/me.json). Access is "pending" or
// "approved"; Avatar is C2's shape and TimeZone an IANA id or null; a rejected person never reaches this.
// PendingRequests is 0 for a non-admin. AdminMissing is true while no admin exists.
public sealed record MeView(
    string Email,
    string Name,
    string? PictureUrl,
    AvatarView Avatar,
    string? TimeZone,
    string Access,
    bool IsAdmin,
    int PendingRequests,
    bool AdminMissing)
{
    public static MeView From(User user, AvatarView avatar, string access, bool isAdmin, int pendingRequests, bool adminMissing) =>
        new(user.Email, user.Name, user.PictureUrl, avatar, user.TimeZone, access, isAdmin, pendingRequests, adminMissing);
}
