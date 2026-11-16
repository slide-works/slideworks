namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// GET /api/signin/options: what the sign-in page offers. Google is false when sign-in isn't set up on this server.
public sealed record SignInOptionsView(bool Google);
