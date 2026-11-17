using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Tests.OAuth.Token;

// Wraps the registered IUserAccounts and runs RevokeInterleave's step just before an access revoke reaches the store, so
// a test can land a request in the gap between the admin ending the person's agent tokens and the person becoming
// Rejected. Timing only: every call still goes to the real store.
public sealed class InterleavedRevoke(IUserAccounts inner, RevokeInterleave interleave) : IUserAccounts
{
    public static void AddTo(IServiceCollection services)
    {
        var registered = services.Last(s => s.ServiceType == typeof(IUserAccounts));
        var implementation = registered.ImplementationType ?? throw new InvalidOperationException("IUserAccounts should be registered by type.");
        services.AddSingleton<RevokeInterleave>();
        services.Add(new ServiceDescriptor(implementation, implementation, registered.Lifetime));
        services.Add(new ServiceDescriptor(
            typeof(IUserAccounts),
            provider => new InterleavedRevoke((IUserAccounts)provider.GetRequiredService(implementation), provider.GetRequiredService<RevokeInterleave>()),
            registered.Lifetime));
    }

    public Task<IReadOnlyList<User>> ListAsync() => inner.ListAsync();

    public Task<User?> FindAsync(Guid id) => inner.FindAsync(id);

    public Task<int> PendingCountAsync() => inner.PendingCountAsync();

    public Task<bool> AnyAdminAsync() => inner.AnyAdminAsync();

    public Task<AccountChange> ApproveAsync(Guid id, Guid? by) => inner.ApproveAsync(id, by);

    public Task<AccountChange> RejectAsync(Guid id, Guid? by) => inner.RejectAsync(id, by);

    public async Task<AccountChange> RevokeAsync(Guid id, Guid? by)
    {
        await interleave.RunAsync();
        return await inner.RevokeAsync(id, by);
    }

    public Task<AccountChange> MakeAdminAsync(Guid id, Guid? by) => inner.MakeAdminAsync(id, by);

    public Task<AccountChange> RemoveAdminAsync(Guid id, Guid? by) => inner.RemoveAdminAsync(id, by);
}
