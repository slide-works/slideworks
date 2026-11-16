namespace SlideWorks.WebApi.Infrastructure.Setup;

// How people and the browser sign in: Program.cs calls these two hooks, and everything about authentication lives here.
public static class AuthSetup
{
    // TODO: register cookie authentication, the fallback policy and the agent policy here.
    public static IServiceCollection AddSlideWorksAuth(this IServiceCollection services)
    {
        services.AddAuthentication();
        services.AddAuthorization();
        return services;
    }

    // Authentication and authorization run after static files and routing, so endpoint metadata such as AllowAnonymous
    // applies. TODO: replace the static bearer token with cookie authentication.
    public static WebApplication UseSlideWorksAuth(this WebApplication app)
    {
        app.UseMiddleware<BearerTokenAuthMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
