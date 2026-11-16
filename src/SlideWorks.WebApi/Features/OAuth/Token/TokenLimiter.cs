using Agoda.IoC.Core;
using SlideWorks.WebApi.Infrastructure.RateLimits;

namespace SlideWorks.WebApi.Features.OAuth.Token;

// POST /oauth/token and /oauth/revoke are anonymous, and each failed redeem or refresh takes SQLite's write lock, so a
// flood from one caller would stall every deck write. Each caller gets one budget per minute for both endpoints. An
// agent redeems once and refreshes about hourly, so the budget only bites on a flood.
[RegisterSingleton(Concrete = true)]
public class TokenLimiter(TimeProvider clock) : FixedWindowLimiter(clock, PER_MINUTE)
{
    public const int PER_MINUTE = 60;
}
