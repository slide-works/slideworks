namespace SlideWorks.WebApi.Features.OAuth.Registration;

// A refused registration, written as an RFC 7591 error body by OAuthRegistrationErrorAttribute. RetryAfter, when set,
// becomes the Retry-After header of a 429.
public sealed class OAuthRegistrationException(int status, string error, string description, TimeSpan? retryAfter = null) : Exception(description)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;

    public int Status { get; } = status;

    public string Error { get; } = error;
}
