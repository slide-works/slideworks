using System.Net.Mime;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Infrastructure.Setup;

// How people and the browser sign in (K1): Program.cs calls these two hooks, and everything about authentication lives
// here. A person has a cookie session; every endpoint needs one unless it is marked AllowAnonymous. Agents on /mcp use
// their own bearer scheme and policy, which McpSetup registers.
public static class AuthSetup
{
    public const string NOT_SIGNED_IN = "Not signed in";

    private static readonly TimeSpan SESSION_LIFETIME = TimeSpan.FromDays(14);

    public static IServiceCollection AddSlideWorksAuth(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = "sw-session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                // Secure whenever the request is HTTPS. PublicOriginMiddleware makes the request's scheme the public URL's,
                // so an https SLIDEWORKS_PUBLIC_URL gives a Secure cookie even though TLS ends at a proxy in front.
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = SESSION_LIFETIME;
                options.SlidingExpiration = true;
                // The SPA owns the sign-in page, so the API never redirects: signed out is a 401 and the client sends the
                // person to /signin.
                options.Events.OnRedirectToLogin = context => WriteErrorAsync(context.Response, StatusCodes.Status401Unauthorized, NOT_SIGNED_IN);
                options.Events.OnRedirectToAccessDenied = context => WriteErrorAsync(context.Response, StatusCodes.Status403Forbidden, "Not allowed");
            });
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        // The session cookie is encrypted with the data-protection key ring. Outside Development it is kept on the data
        // volume next to the database, so a restart or a new image doesn't sign everyone out. Development keeps the
        // framework's per-user key store.
        services.AddDataProtection();
        services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, IHostEnvironment, ILoggerFactory>(
            (options, configuration, environment, loggerFactory) =>
            {
                if (!environment.IsDevelopment())
                {
                    options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(KeysDirectory(configuration)), loggerFactory);
                }
            });
        return services;
    }

    // Authentication and authorization run after static files and routing, so endpoint metadata such as AllowAnonymous
    // applies. PublicOriginMiddleware comes first, so every cookie, the write guard and any sign-in callback URL see the
    // public URL's scheme and host. The write guard sits between authentication and authorization: it needs to know
    // whether the caller is signed in, and it must refuse a cross-site write before any endpoint runs.
    public static WebApplication UseSlideWorksAuth(this WebApplication app)
    {
        app.UseMiddleware<PublicOriginMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<CrossSiteWriteGuard>();
        app.UseAuthorization();
        return app;
    }

    private static string KeysDirectory(IConfiguration configuration)
    {
        var dbPath = configuration["SLIDEWORKS_DB_PATH"] ?? "/data/slide-works.db";
        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath))!, "slide-works-keys");
    }

    private static Task WriteErrorAsync(HttpResponse response, int status, string error)
    {
        response.StatusCode = status;
        response.ContentType = MediaTypeNames.Application.Json;
        return response.WriteAsJsonAsync(new ErrorResponse(error));
    }
}
