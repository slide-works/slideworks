namespace SlideWorks.WebApi.Features.OAuth.Authorize;

// The request is expired, already answered, or someone else's. Mapped to 410 by ConsentGoneAttribute.
public sealed class ConsentGoneException() : Exception("This request has expired or was already answered. Go back to your agent and connect again.")
{
}
