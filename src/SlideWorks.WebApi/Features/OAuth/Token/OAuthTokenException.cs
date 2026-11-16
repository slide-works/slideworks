namespace SlideWorks.WebApi.Features.OAuth.Token;

// A refused token or revoke request, written as an RFC 6749 error body by OAuthTokenErrorAttribute.
public sealed class OAuthTokenException(int status, string error, string description) : Exception(description)
{
    public int Status { get; } = status;

    public string Error { get; } = error;
}
