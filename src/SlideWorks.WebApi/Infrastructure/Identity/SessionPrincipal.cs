using System.Security.Claims;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Infrastructure.Identity;

// Builds the principal of a signed-in person (S2), from the stored user, so every way a person is signed in carries the
// same SlideWorksClaims: the Google sign-in, the cookie scheme's per-request refresh and the test scheme.
public static class SessionPrincipal
{
    public const string IS_ADMIN_VALUE = "true";

    // Throws for a Rejected user, who gets no session: callers check Access first.
    public static ClaimsPrincipal For(User user, string scheme)
    {
        if (user.Access == UserAccess.Rejected)
        {
            throw new InvalidOperationException($"User {user.Id} is rejected and can't have a session.");
        }
        var claims = new List<Claim>
        {
            new(SlideWorksClaims.PRESENTER_ID, user.Id.ToString()),
            new(SlideWorksClaims.EMAIL, user.Email),
            new(SlideWorksClaims.NAME, user.Name),
            new(SlideWorksClaims.ACTOR_KIND, nameof(ActorKind.Person)),
            new(SlideWorksClaims.ACCESS, user.Access.ToString()),
        };
        if (user.IsAdmin)
        {
            claims.Add(new Claim(SlideWorksClaims.IS_ADMIN, IS_ADMIN_VALUE));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }
}
