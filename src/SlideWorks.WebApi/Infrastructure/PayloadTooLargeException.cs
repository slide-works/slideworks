namespace SlideWorks.WebApi.Infrastructure;

// Thrown by services when a request body is over its limit; mapped to 413 by ApiExceptionFilterAttribute.
public sealed class PayloadTooLargeException(string message) : Exception(message)
{
}
