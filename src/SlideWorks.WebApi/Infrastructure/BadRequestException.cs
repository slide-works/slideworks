namespace SlideWorks.WebApi.Infrastructure;

// Thrown by services when a request payload references data that does not
// exist or violates a domain rule; mapped to 400 by ApiExceptionFilterAttribute.
public sealed class BadRequestException(string message) : Exception(message)
{
}
