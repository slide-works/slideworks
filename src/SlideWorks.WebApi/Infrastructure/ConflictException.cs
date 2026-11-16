namespace SlideWorks.WebApi.Infrastructure;

// Thrown by services when a request is valid in shape but conflicts with the
// current state of the resource; mapped to 409 by ApiExceptionFilterAttribute.
public sealed class ConflictException(string message) : Exception(message)
{
}
