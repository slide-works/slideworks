using Microsoft.AspNetCore.Mvc;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// The form parameter of POST /oauth/revoke that SlideWorks reads. token_type_hint and client_id are ignored: either kind
// of token is found by its hash, and holding the token is what lets a caller revoke it.
public sealed class RevokeRequest
{
    [FromForm(Name = "token")]
    public string? Token { get; init; }
}
