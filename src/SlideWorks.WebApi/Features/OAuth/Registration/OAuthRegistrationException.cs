namespace SlideWorks.WebApi.Features.OAuth.Registration;

// A refused registration, written as an RFC 7591 error body by OAuthRegistrationErrorAttribute.
public sealed class OAuthRegistrationException(int status, string error, string description) : Exception(description)
{
    public int Status { get; } = status;

    public string Error { get; } = error;
}
