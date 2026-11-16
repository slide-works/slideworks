namespace SlideWorks.WebApi.Features.OAuth.Stores;

// Who a valid access token acts for: the presenter, and the client acting for them by its client_name.
public sealed record AgentPrincipal(Guid PresenterId, string Email, string ClientName);
