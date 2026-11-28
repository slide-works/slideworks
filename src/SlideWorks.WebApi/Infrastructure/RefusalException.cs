namespace SlideWorks.WebApi.Infrastructure;

// Thrown by services to refuse with a status, a code and a worded message; ApiExceptionFilterAttribute maps it to that
// status with a RefusalResponse body.
public sealed class RefusalException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;

    public string Code { get; } = code;
}
