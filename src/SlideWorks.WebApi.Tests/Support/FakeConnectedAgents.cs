using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.OAuth.ConnectedAgents;

namespace SlideWorks.WebApi.Tests.Support;

// IConnectedAgents for tests of the profile's agents array before the real store exists: every person has the same two
// agents, one connected before the connect time was kept. Add it with AddTo.
public sealed class FakeConnectedAgents : IConnectedAgents
{
    public static readonly ConnectedAgent Claude = new(Guid.Parse("3f2b7c84-1d5e-4a90-8b6f-9e0d4c7a2b15"), "Claude", "3 days ago", "2 min ago");

    public static readonly ConnectedAgent Cursor = new(Guid.Parse("8a1e5d36-7c42-4f09-b2d8-5e6f0a9c3d71"), "Cursor", "Unknown", null);

    public static void AddTo(IServiceCollection services) => services.AddSingleton<IConnectedAgents, FakeConnectedAgents>();

    public Task<IReadOnlyList<ConnectedAgent>> ListAsync(Guid presenterId) => Task.FromResult<IReadOnlyList<ConnectedAgent>>([Claude, Cursor]);

    public Task<bool> DisconnectAsync(Guid presenterId, Guid familyId) => Task.FromResult(false);
}
