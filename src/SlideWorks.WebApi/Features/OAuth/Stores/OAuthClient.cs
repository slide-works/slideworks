namespace SlideWorks.WebApi.Features.OAuth.Stores;

// A registered client. ClientName is what people see on the consent screen and in the change log, such as "Claude".
public sealed record OAuthClient(string ClientId, string ClientName, IReadOnlyList<string> RedirectUris, DateTimeOffset CreatedAt);
