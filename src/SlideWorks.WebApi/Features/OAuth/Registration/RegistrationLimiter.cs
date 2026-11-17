using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure.RateLimits;

namespace SlideWorks.WebApi.Features.OAuth.Registration;

// Registration is anonymous, so each caller gets a small budget per minute.
[RegisterSingleton(Concrete = true)]
public class RegistrationLimiter(TimeProvider clock) : FixedWindowLimiter(clock, PER_MINUTE)
{
    public const int PER_MINUTE = 10;
}
