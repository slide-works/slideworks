namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// Where the browser goes next: the client's redirect URI with code and state, or with error=access_denied and state.
public sealed record ConsentDecisionView(string Redirect);
