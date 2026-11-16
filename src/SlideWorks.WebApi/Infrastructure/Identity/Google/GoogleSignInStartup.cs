using Agoda.IoC.Core;
using Microsoft.AspNetCore.Authentication;

namespace SlideWorks.WebApi.Infrastructure.Identity.Google;

// With sign-in not set up, removes the Google scheme before the server takes requests: a remote scheme's handler runs on
// every request, and it can't be built without a client id.
[RegisterSingleton(For = typeof(IHostedService))]
public sealed class GoogleSignInStartup(GoogleSignInSettings settings, IAuthenticationSchemeProvider schemes, ILogger<GoogleSignInStartup> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (settings.IsConfigured)
        {
            logger.LogInformation("Sign-in with Google is on");
        }
        else
        {
            logger.LogWarning("Sign-in with Google is off: set {ClientId} and {ClientSecret}", GoogleSignInSettings.CLIENT_ID_KEY, GoogleSignInSettings.CLIENT_SECRET_KEY);
            schemes.RemoveScheme(GoogleSignInSetup.SCHEME);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
