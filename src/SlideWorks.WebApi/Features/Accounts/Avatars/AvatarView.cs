namespace SlideWorks.WebApi.Features.Accounts.Avatars;

// How a person is shown everywhere (C2). PictureUrl is the uploaded photo's URL, else the Google picture, else null.
public sealed record AvatarView(string Initials, string Colour, string? PictureUrl);
