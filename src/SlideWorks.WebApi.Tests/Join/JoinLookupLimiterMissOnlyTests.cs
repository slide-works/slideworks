using Microsoft.Extensions.Time.Testing;
using SlideWorks.WebApi.Infrastructure.RateLimits;

namespace SlideWorks.WebApi.Tests.Join;

// The limiter's miss-only path (OverMissLimit, RecordMiss), used by the guest poll routes, on a fake clock with no host: it
// shares the miss budget with Allow and Miss, and never takes or releases an in-flight slot.
[TestFixture]
public class JoinLookupLimiterMissOnlyTests
{
    private const string CALLER = "203.0.113.7";

    [Test]
    public void OverMissLimit_WhenTheMissesAreRecordedOnTheMissOnlyPath_ShouldTurnTrueAndAllowShouldRefuse()
    {
        var limiter = new JoinLookupLimiter(new FakeTimeProvider());

        for (var i = 0; i < JoinLookupLimiter.MAX_MISSES; i++)
        {
            limiter.OverMissLimit(CALLER).ShouldBeFalse($"after {i} misses");
            limiter.RecordMiss(CALLER);
        }

        limiter.OverMissLimit(CALLER).ShouldBeTrue();
        limiter.Allow(CALLER).ShouldBeFalse();
    }

    [Test]
    public void OverMissLimit_WhenTheMissesCameThroughAllowAndMiss_ShouldBeTrue()
    {
        var limiter = new JoinLookupLimiter(new FakeTimeProvider());

        for (var i = 0; i < JoinLookupLimiter.MAX_MISSES; i++)
        {
            limiter.Allow(CALLER).ShouldBeTrue();
            limiter.Miss(CALLER);
        }

        limiter.OverMissLimit(CALLER).ShouldBeTrue();
    }

    [Test]
    public void OverMissLimit_WhenTheWindowEnds_ShouldBeFalse()
    {
        var clock = new FakeTimeProvider();
        var limiter = new JoinLookupLimiter(clock);
        for (var i = 0; i < JoinLookupLimiter.MAX_MISSES; i++)
        {
            limiter.RecordMiss(CALLER);
        }

        clock.Advance(TimeSpan.FromMinutes(1));

        limiter.OverMissLimit(CALLER).ShouldBeFalse();
        limiter.Allow(CALLER).ShouldBeTrue();
    }

    // The miss-only path neither takes a slot (a room's reads never fill the cap) nor frees one (another lookup's slot stays
    // held until it settles).
    [Test]
    public void MissOnlyPath_WhenLookupsAreInFlight_ShouldNeitherTakeNorFreeASlot()
    {
        var limiter = new JoinLookupLimiter(new FakeTimeProvider());
        for (var i = 0; i < 10 * JoinLookupLimiter.MAX_IN_FLIGHT; i++)
        {
            limiter.OverMissLimit(CALLER).ShouldBeFalse();
        }
        for (var i = 0; i < JoinLookupLimiter.MAX_IN_FLIGHT; i++)
        {
            limiter.Allow(CALLER).ShouldBeTrue($"lookup {i + 1}");
        }

        limiter.RecordMiss(CALLER);

        limiter.Allow(CALLER).ShouldBeFalse();
        limiter.Hit(CALLER);
        limiter.Allow(CALLER).ShouldBeTrue();
    }

    // A miss recorded after the window ended starts a new window that still holds the lookups in flight from the old one.
    [Test]
    public void RecordMiss_WhenTheWindowEndedWithLookupsInFlight_ShouldCarryThemAndCountOneMiss()
    {
        var clock = new FakeTimeProvider();
        var limiter = new JoinLookupLimiter(clock);
        for (var i = 0; i < JoinLookupLimiter.MAX_IN_FLIGHT; i++)
        {
            limiter.Allow(CALLER).ShouldBeTrue();
        }
        clock.Advance(TimeSpan.FromMinutes(1));

        limiter.RecordMiss(CALLER);

        limiter.Allow(CALLER).ShouldBeFalse();
        for (var i = 1; i < JoinLookupLimiter.MAX_MISSES; i++)
        {
            limiter.RecordMiss(CALLER);
        }
        limiter.OverMissLimit(CALLER).ShouldBeTrue();
    }
}
