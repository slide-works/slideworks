using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Accounts.Admin;

// At startup, warns when nobody can approve sign-ups: no admin exists and SLIDEWORKS_ADMIN_EMAIL is unset. The server
// still starts. The database is migrated before hosted services start.
[RegisterSingleton(For = typeof(IHostedService))]
public sealed class NoAdminWarning(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NoAdminWarning> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(configuration[IUserDirectory.ADMIN_EMAIL_SETTING]))
        {
            return;
        }
        await using var scope = scopes.CreateAsyncScope();
        if (!await scope.ServiceProvider.GetRequiredService<IUserAccounts>().AnyAdminAsync())
        {
            logger.LogWarning("No admin exists and {Setting} is not set, so nobody can approve sign-ups. Set it and sign in with that Google account.", IUserDirectory.ADMIN_EMAIL_SETTING);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
