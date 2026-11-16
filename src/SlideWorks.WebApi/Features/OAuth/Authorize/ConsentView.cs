namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// The consent screen: which agent asks, and for what.
public sealed record ConsentView(string ClientName, string Scope);
