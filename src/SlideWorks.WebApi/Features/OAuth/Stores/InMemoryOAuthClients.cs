using System.Collections.Concurrent;
using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Features.OAuth.Stores;

// TODO: replace with the EF store on OAuthClientRow; registrations are lost on restart until then.
[RegisterSingleton(For = typeof(IOAuthClients))]
public class InMemoryOAuthClients(TimeProvider clock) : IOAuthClients
{
    private readonly ConcurrentDictionary<string, OAuthClient> _clients = new(StringComparer.Ordinal);

    public Task<OAuthClient> RegisterAsync(ClientRegistration registration)
    {
        var client = new OAuthClient(OAuthSecrets.New(), registration.ClientName, [.. registration.RedirectUris], clock.GetUtcNow());
        _clients[client.ClientId] = client;
        return Task.FromResult(client);
    }

    public Task<OAuthClient?> FindAsync(string clientId) => Task.FromResult(_clients.GetValueOrDefault(clientId));
}
