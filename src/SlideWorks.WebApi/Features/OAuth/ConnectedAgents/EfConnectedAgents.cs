using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.OAuth.ConnectedAgents;

// TODO: read the person's live token families from AgentTokens and revoke one on disconnect.
[RegisterPerRequest]
public sealed class EfConnectedAgents : IConnectedAgents
{
    public Task<IReadOnlyList<ConnectedAgent>> ListAsync(Guid presenterId) => Task.FromResult<IReadOnlyList<ConnectedAgent>>([]);

    public Task<bool> DisconnectAsync(Guid presenterId, Guid familyId) => Task.FromResult(false);
}
