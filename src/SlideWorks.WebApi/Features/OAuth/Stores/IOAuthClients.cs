namespace SlideWorks.WebApi.Features.OAuth.Stores;

// The clients agents register through dynamic client registration, the only way a client comes to exist.
public interface IOAuthClients
{
    // Gives the client a new random client_id.
    Task<OAuthClient> RegisterAsync(ClientRegistration registration);

    // Null when no client has that id.
    Task<OAuthClient?> FindAsync(string clientId);
}
