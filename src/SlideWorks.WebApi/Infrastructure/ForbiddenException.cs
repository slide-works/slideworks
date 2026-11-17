namespace SlideWorks.WebApi.Infrastructure;

// The caller can see the thing but may not do this to it: 403. Anything they can't see is NotFoundException (404).
public sealed class ForbiddenException(string message) : Exception(message)
{
}
