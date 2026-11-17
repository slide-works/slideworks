using Microsoft.Extensions.Time.Testing;
using SlideWorks.WebApi.Infrastructure.RateLimits;

namespace SlideWorks.WebApi.Tests.Join;

// The real limiter on a fake clock, with no host: the in-flight cap and the window boundary, which the HTTP tests can't reach
// deterministically (D4).
[TestFixture]
public class JoinLookupLimiterTests
{
    private const string CALLER = "203.0.113.7";

    [Test]
    public void Allow_WhenInFlightReachesTheCap_ShouldRefuseUntilALookupSettlesOrTheWindowEnds()
    {
        var clock = new FakeTimeProvider();
        var limiter = new JoinLookupLimiter(clock);

        for (var i = 0; i < JoinLookupLimiter.MAX_IN_FLIGHT; i++)
        {
            limiter.Allow(CALLER).ShouldBeTrue($"lookup {i + 1} of {JoinLookupLimiter.MAX_IN_FLIGHT}");
        }
        limiter.Allow(CALLER).ShouldBeFalse();

        limiter.Hit(CALLER);
        limiter.Allow(CALLER).ShouldBeTrue();
        limiter.Allow(CALLER).ShouldBeFalse();

        clock.Advance(TimeSpan.FromMinutes(1));
        limiter.Allow(CALLER).ShouldBeTrue();
    }

    [Test]
    public void Miss_WhenAHitSettlesAfterTheWindowChanged_ShouldNotDropTheNewWindowsMisses()
    {
        var clock = new FakeTimeProvider();
        var limiter = new JoinLookupLimiter(clock);
        limiter.Allow(CALLER).ShouldBeTrue();
        clock.Advance(TimeSpan.FromMinutes(1));
        limiter.Allow(CALLER).ShouldBeTrue();

        limiter.Hit(CALLER);
        limiter.Miss(CALLER);
        for (var i = 1; i < JoinLookupLimiter.MAX_MISSES; i++)
        {
            limiter.Allow(CALLER).ShouldBeTrue();
            limiter.Miss(CALLER);
        }

        limiter.Allow(CALLER).ShouldBeFalse();
    }

    [Test]
    public void Allow_WhenALookupNeverSettled_ShouldNotHoldItsSlotBeyondOneMoreWindow()
    {
        var clock = new FakeTimeProvider();
        var limiter = new JoinLookupLimiter(clock);
        limiter.Allow(CALLER).ShouldBeTrue();

        for (var window = 0; window < 3; window++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            limiter.Allow(CALLER).ShouldBeTrue();
            limiter.Hit(CALLER);
        }
        for (var i = 1; i <= JoinLookupLimiter.MAX_IN_FLIGHT; i++)
        {
            limiter.Allow(CALLER).ShouldBeTrue($"lookup {i}");
        }

        limiter.Allow(CALLER).ShouldBeFalse();
    }
}
