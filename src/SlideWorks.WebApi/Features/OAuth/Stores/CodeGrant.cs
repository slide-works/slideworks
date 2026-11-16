namespace SlideWorks.WebApi.Features.OAuth.Stores;

// What a redeemed authorization code grants: the client acts for the presenter, with this scope, at this resource.
public sealed record CodeGrant(string ClientId, Guid PresenterId, string Scope, string Resource);
