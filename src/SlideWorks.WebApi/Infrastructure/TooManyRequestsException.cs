namespace SlideWorks.WebApi.Infrastructure;

// Thrown by services when an anonymous caller has used up its budget; mapped to 429 by ApiExceptionFilterAttribute. The
// message is the error code the client reads, such as "too_many_attempts".
public sealed class TooManyRequestsException(string message) : Exception(message)
{
}
