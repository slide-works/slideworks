using SlideWorks.WebApi.Features.Accounts.Avatars;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// One person on a deck's list (C4). Name and Avatar are null for an email with no account (D24). Access is "read" or "write".
public sealed record SharingPersonView(string Email, string? Name, AvatarView? Avatar, string Access);
