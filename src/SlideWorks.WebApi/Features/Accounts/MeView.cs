namespace SlideWorks.WebApi.Features.Accounts;

// GET /api/me: the signed-in person, for the shell's user menu (src/contracts/screens/me.json).
public sealed record MeView(string Email, string Name, string? PictureUrl)
{
    public static MeView From(User user) => new(user.Email, user.Name, user.PictureUrl);
}
