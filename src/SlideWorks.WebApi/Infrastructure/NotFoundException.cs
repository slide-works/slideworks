namespace SlideWorks.WebApi.Infrastructure;

// Thrown by services when a routed id does not exist; mapped to 404 by
// ApiExceptionFilterAttribute.
public sealed class NotFoundException(string message) : Exception(message)
{
}
