using System.Net.Mime;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Infrastructure.Identity.Gate;
using SlideWorks.WebApi.Infrastructure.Identity.Google;

namespace SlideWorks.WebApi.Infrastructure.Setup;

// How people and the browser sign in (K1): Program.cs calls these two hooks, and everything about authentication lives
// here. A person has a cookie session; every endpoint needs an approved person unless it is marked AllowAnonymous or
// names another policy (S3): SignedIn admits a pending person too, and Admin only an admin. Agents on /mcp use their own
// bearer scheme and policy, which McpSetup registers.
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
                options.Events.OnValidatePrincipal = FollowCurrentAccessAsync;
            });
        services.AddGoogleSignIn();
        // The fallback (no authorization metadata) and the default (a bare [Authorize]) are both the approved gate, so no
        // endpoint lets a pending person in by forgetting a policy. GateResultHandler writes the 403 bodies.
        var approvedPerson = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new ApprovedRequirement()).Build();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(approvedPerson)
            .SetDefaultPolicy(approvedPerson)
            .AddPolicy(SignedInPolicy.NAME, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(AdminPolicy.NAME, policy => policy.RequireAuthenticatedUser().AddRequirements(new ApprovedRequirement(), new AdminRequirement()));
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

    // A cookie session follows the person's current access on every request (S2, D8): one read by id, no cache. Missing or
    // Rejected signs the cookie out, so the request is signed out. Otherwise the principal is rebuilt from the stored user,
    // so an approval or an admin change applies on the next request; the cookie itself isn't re-issued (ShouldRenew stays
    // false).
    private static async Task FollowCurrentAccessAsync(CookieValidatePrincipalContext context)
    {
        var user = Guid.TryParse(context.Principal?.FindFirstValue(SlideWorksClaims.PRESENTER_ID), out var id)
            ? await context.HttpContext.RequestServices.GetRequiredService<IUserAccounts>().FindAsync(id)
            : null;
        if (user == null || user.Access == UserAccess.Rejected)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        context.ReplacePrincipal(SessionPrincipal.For(user, CookieAuthenticationDefaults.AuthenticationScheme));
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
