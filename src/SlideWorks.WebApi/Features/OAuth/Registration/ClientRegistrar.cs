using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.OAuth.Stores;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// Dynamic client registration (RFC 7591), the only way a client comes to exist (D20). Client metadata documents are not
// fetched: that would make the server request URLs a stranger chose.
[RegisterPerRequest(Concrete = true)]
public class ClientRegistrar(IOAuthClients clients, RegistrationLimiter limiter)
{
    public const int MAX_NAME_LENGTH = 60;

    public async Task<ClientRegistrationView> RegisterAsync(ClientRegistrationRequest request, string callerKey)
    {
        if (!limiter.TryAcquire(callerKey))
        {
            throw new OAuthRegistrationException(StatusCodes.Status429TooManyRequests, "too_many_requests", $"At most {RegistrationLimiter.PER_MINUTE} registrations a minute from one address. Try again shortly.");
        }
        var name = request.ClientName?.Trim() ?? "";
        if (name.Length == 0 || name.Length > MAX_NAME_LENGTH || name.Any(char.IsControl))
        {
            throw new OAuthRegistrationException(StatusCodes.Status400BadRequest, "invalid_client_metadata", $"client_name is required, at most {MAX_NAME_LENGTH} characters.");
        }
        if (request.RedirectUris is not { Count: > 0 and <= RedirectUriRules.MAX_REDIRECTS })
        {
            throw new OAuthRegistrationException(StatusCodes.Status400BadRequest, "invalid_redirect_uri", $"Give between 1 and {RedirectUriRules.MAX_REDIRECTS} redirect_uris.");
        }
        if (!request.RedirectUris.All(RedirectUriRules.IsAcceptable))
        {
            throw new OAuthRegistrationException(StatusCodes.Status400BadRequest, "invalid_redirect_uri", "Each redirect URI must be https, or http on localhost, 127.0.0.1 or [::1], with no fragment.");
        }
        return ClientRegistrationView.From(await clients.RegisterAsync(new ClientRegistration(name, [.. request.RedirectUris])));
    }
}
