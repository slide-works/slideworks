using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.RateLimits;

// Who a rate limit counts a call against, on a host with no SLIDEWORKS_TRUSTED_PROXIES (the default): the connection's
// remote address, with an IPv4-mapped IPv6 address taken as its IPv4 and an IPv6 address taken as its /64. Both anonymous
// limits, token and registration, key the same way. X-Forwarded-For changes nothing here.
[TestFixture]
public class CallerKeyTests : ApiTestFixture
{
    private RateLimitProbe _probe = null!;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Action<IServiceCollection>? ConfigureTestServices => TestRemoteAddress.AddTo;

    [OneTimeSetUp]
    public void CreateProbe() => _probe = new RateLimitProbe(Client);

    [SetUp]
    public void NextWindow() => Clock.Advance(TimeSpan.FromMinutes(1));

    [TestCase(LimitedEndpoint.Token)]
    [TestCase(LimitedEndpoint.Registration)]
    public async Task Limit_WhenTwoAddresses_ShouldGiveEachItsOwnBudget(LimitedEndpoint endpoint)
    {
        var budget = RateLimitProbe.BudgetOf(endpoint);

        (await _probe.SendAllAsync(endpoint, Repeat(("198.51.100.1", null), budget + 1))).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, budget + 1));

        (await _probe.SendAllAsync(endpoint, Repeat(("198.51.100.2", null), budget + 1))).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, budget + 1));
    }

    // An IPv6 host holds a whole /64, so rotating through it buys nothing; the next /64 is someone else.
    [TestCase(LimitedEndpoint.Token)]
    [TestCase(LimitedEndpoint.Registration)]
    public async Task Limit_WhenAddressesShareAnIpv6Slash64_ShouldShareOneBudget(LimitedEndpoint endpoint)
    {
        var callers = Enumerable.Range(1, 2 * RateLimitProbe.BudgetOf(endpoint))
            .Select(i => ((string?)$"2001:db8:1:2:{i:x}::{i:x}", (string?)null))
            .ToList();

        (await _probe.SendAllAsync(endpoint, callers)).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, callers.Count));

        (await _probe.SendAsync(endpoint, "2001:db8:1:3::1")).ShouldBe(RateLimitProbe.Served(endpoint));
    }

    // A dual-stack socket reports an IPv4 client as ::ffff:a.b.c.d; it is the same caller as a.b.c.d.
    [TestCase(LimitedEndpoint.Token)]
    [TestCase(LimitedEndpoint.Registration)]
    public async Task Limit_WhenIpv4MappedAndPlainIpv4_ShouldShareOneBudget(LimitedEndpoint endpoint)
    {
        var callers = Enumerable.Range(0, RateLimitProbe.BudgetOf(endpoint) + 1)
            .Select(i => ((string?)(i % 2 == 0 ? "::ffff:198.51.100.7" : "198.51.100.7"), (string?)null))
            .ToList();

        (await _probe.SendAllAsync(endpoint, callers)).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, callers.Count));
    }

    // Without SLIDEWORKS_TRUSTED_PROXIES no proxy is trusted, so a changing X-Forwarded-For can't buy a new budget.
    [TestCase(LimitedEndpoint.Token)]
    [TestCase(LimitedEndpoint.Registration)]
    public async Task Limit_WhenNoTrustedProxiesAndForwardedForChanges_ShouldIgnoreIt(LimitedEndpoint endpoint)
    {
        var callers = Enumerable.Range(1, RateLimitProbe.BudgetOf(endpoint) + 1)
            .Select(i => ((string?)"10.0.0.1", (string?)$"198.51.100.{i}"))
            .ToList();

        (await _probe.SendAllAsync(endpoint, callers)).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, callers.Count));
    }

    private static List<(string? Remote, string? ForwardedFor)> Repeat((string? Remote, string? ForwardedFor) caller, int count)
        => Enumerable.Repeat(caller, count).ToList();
}
