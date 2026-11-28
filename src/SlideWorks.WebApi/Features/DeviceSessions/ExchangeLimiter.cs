using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure.RateLimits;

namespace SlideWorks.WebApi.Features.DeviceSessions;

// POST /api/auth/device-sessions/exchange is anonymous, and every attempt takes SQLite's write lock (a known code is marked
// used whatever the outcome), so a flood from one caller would stall every deck write. A phone exchanges once per sign-in,
// so the budget only bites on a flood; over it the exchange is 429 too_many_attempts (K4).
[RegisterSingleton(Concrete = true)]
public class ExchangeLimiter(TimeProvider clock) : FixedWindowLimiter(clock, PER_MINUTE)
{
    public const int PER_MINUTE = 10;
}
