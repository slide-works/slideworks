namespace SlideWorks.WebApi.Infrastructure.RequestLimits;

// The action reads its body as a stream with a cap of its own (an image upload counts its bytes and answers its own 413),
// so the shared limit leaves it alone. An action with a [RequestSizeLimit] is left alone the same way.
[AttributeUsage(AttributeTargets.Method)]
public sealed class OwnsBodyLimitAttribute : Attribute
{
    // A marker: the middleware looks for the attribute, there is nothing to configure.
}
