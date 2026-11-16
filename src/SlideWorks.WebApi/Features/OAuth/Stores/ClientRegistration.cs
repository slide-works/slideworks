namespace SlideWorks.WebApi.Features.OAuth.Stores;

// What a client sends to dynamic client registration, once the endpoint has checked it.
public sealed record ClientRegistration(string ClientName, IReadOnlyList<string> RedirectUris);
