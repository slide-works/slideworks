using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SlideWorks.WebApi.Infrastructure;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.OAuth.Metadata;

// What an agent reads before it has any credentials: /mcp's 401 points at the protected-resource document, which names
// this server as the authorization server, whose document lists the endpoints. Every URL comes from IPublicUrls, so a
// forged Host header never reaches them. The /mcp suffix is the RFC 9728 path-inserted form some clients try first.
[AllowAnonymous]
[Route(".well-known")]
public class OAuthMetadataController(IPublicUrls urls) : ApiControllerBase
{
    [HttpGet("oauth-protected-resource")]
    [HttpGet("oauth-protected-resource/mcp")]
    [ProducesResponseType<ProtectedResourceMetadataView>(StatusCodes.Status200OK)]
    public ProtectedResourceMetadataView ProtectedResource() => ProtectedResourceMetadataView.From(urls.Base, urls.Mcp);

    [HttpGet("oauth-authorization-server")]
    [ProducesResponseType<AuthorizationServerMetadataView>(StatusCodes.Status200OK)]
    public AuthorizationServerMetadataView AuthorizationServer() => AuthorizationServerMetadataView.From(urls.Base);
}
