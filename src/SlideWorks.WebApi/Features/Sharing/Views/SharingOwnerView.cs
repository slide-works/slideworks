using SlideWorks.WebApi.Features.Accounts.Avatars;

namespace SlideWorks.WebApi.Features.Sharing.Views;

// The deck's owner in the sharing dialog (C4): their email, display name and avatar (C2).
public sealed record SharingOwnerView(string Email, string Name, AvatarView Avatar);
