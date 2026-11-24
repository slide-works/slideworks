namespace SlideWorks.WebApi.Infrastructure;

// Thrown by services when a caller has used up its budget; mapped to 429 by ApiExceptionFilterAttribute. The message is the
// error code the client reads, such as "too_many_attempts", or for sharing the sentence to show. RetryAfter, when set, goes in
// the Retry-After header.
public sealed class TooManyRequestsException(string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
