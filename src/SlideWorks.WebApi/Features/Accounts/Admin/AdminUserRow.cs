using SlideWorks.WebApi.Features.Accounts.Avatars;

namespace SlideWorks.WebApi.Features.Accounts.Admin;

// One account on /admin/users. Access is "pending", "approved" or "rejected". The labels are server-formatted.
// ChangedBy is the admin's name, or null when the system changed it.
public sealed record AdminUserRow(
    Guid Id,
    string Name,
    string Email,
    string? PictureUrl,
    AvatarView Avatar,
    string Access,
    bool IsAdmin,
    bool IsSelf,
    string RequestedLabel,
    string? ChangedLabel,
    string? ChangedBy);
