using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Setup;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.RateLimits;

// SLIDEWORKS_TRUSTED_PROXIES set to a network and a single address: a call from one of them is counted against the client
// in the last X-Forwarded-For entry, so clients behind the proxy each get their own budget. From anywhere else, or from
// no address at all, X-Forwarded-For is ignored. Both anonymous limits, token and registration, behave the same.
[TestFixture]
public class TrustedProxyTests : ApiTestFixture
{
    private const string PROXY = "10.0.0.1";
    private const string IPV6_PROXY = "2001:db8:ffff::1";

    private RateLimitProbe _probe = null!;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override Dictionary<string, string?> ExtraConfig => new() { [TrustedProxiesSetup.CONFIG_KEY] = $" 10.0.0.0/8 , {IPV6_PROXY}" };

    protected override Action<IServiceCollection>? ConfigureTestServices => TestRemoteAddress.AddTo;

    [OneTimeSetUp]
    public void CreateProbe() => _probe = new RateLimitProbe(Client);

    [SetUp]
    public void NextWindow() => Clock.Advance(TimeSpan.FromMinutes(1));

    [TestCase(LimitedEndpoint.Token)]
    [TestCase(LimitedEndpoint.Registration)]
    public async Task Limit_WhenBehindTrustedProxy_ShouldGiveEachForwardedClientItsOwnBudget(LimitedEndpoint endpoint)
    {
        var budget = RateLimitProbe.BudgetOf(endpoint);

        (await _probe.SendAllAsync(endpoint, Repeat((PROXY, "198.51.100.1"), budget + 1))).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, budget + 1));

        (await _probe.SendAllAsync(endpoint, Repeat((PROXY, "198.51.100.2"), budget + 1))).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, budget + 1));
        // The single-address entry is trusted too, and it's the same client whichever proxy it came through.
        (await _probe.SendAsync(endpoint, IPV6_PROXY, "198.51.100.1")).ShouldBe(System.Net.HttpStatusCode.TooManyRequests);
    }

    // A proxy on a dual-stack socket shows up as ::ffff:10.0.0.1, which is still inside 10.0.0.0/8.
    [TestCase(LimitedEndpoint.Token)]
    [TestCase(LimitedEndpoint.Registration)]
    public async Task Limit_WhenTrustedProxyArrivesAsIpv4Mapped_ShouldStillReadForwardedFor(LimitedEndpoint endpoint)
    {
        var budget = RateLimitProbe.BudgetOf(endpoint);

        (await _probe.SendAllAsync(endpoint, Repeat(("::ffff:10.0.0.1", "198.51.100.3"), budget + 1))).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, budget + 1));

        (await _probe.SendAsync(endpoint, "::ffff:10.0.0.1", "198.51.100.4")).ShouldBe(RateLimitProbe.Served(endpoint));
    }

    // A caller outside the trusted list, including the address next to the single trusted one, can't pick its own key.
    [Test]
    public async Task Limit_WhenForwardedForFromUntrustedSource_ShouldIgnoreIt(
        [Values] LimitedEndpoint endpoint,
        [Values("203.0.113.9", "2001:db8:ffff::2")] string remote)
    {
        var callers = Enumerable.Range(1, RateLimitProbe.BudgetOf(endpoint) + 1)
            .Select(i => ((string?)remote, (string?)$"198.51.100.{i}"))
            .ToList();

        (await _probe.SendAllAsync(endpoint, callers)).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, callers.Count));
    }

    // Only one hop is read: the entry the trusted proxy appended. Whatever the client wrote before it is ignored.
    [TestCase(LimitedEndpoint.Token)]
    [TestCase(LimitedEndpoint.Registration)]
    public async Task Limit_WhenForwardedForHasSeveralEntries_ShouldTakeOnlyTheLast(LimitedEndpoint endpoint)
    {
        var callers = Enumerable.Range(1, RateLimitProbe.BudgetOf(endpoint) + 1)
            .Select(i => ((string?)PROXY, (string?)$"203.0.113.{i}, 198.51.100.9"))
            .ToList();

        (await _probe.SendAllAsync(endpoint, callers)).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, callers.Count));
    }

    // The framework would trust X-Forwarded-For on a connection with no remote address; SlideWorks doesn't.
    [TestCase(LimitedEndpoint.Token)]
    [TestCase(LimitedEndpoint.Registration)]
    public async Task Limit_WhenForwardedForWithNoRemoteAddress_ShouldIgnoreIt(LimitedEndpoint endpoint)
    {
        var callers = Enumerable.Range(1, RateLimitProbe.BudgetOf(endpoint) + 1)
            .Select(i => ((string?)null, (string?)$"198.51.100.{i}"))
            .ToList();

        (await _probe.SendAllAsync(endpoint, callers)).ShouldBe(RateLimitProbe.BudgetThenRefused(endpoint, callers.Count));
    }

    private static List<(string? Remote, string? ForwardedFor)> Repeat((string? Remote, string? ForwardedFor) caller, int count)
        => Enumerable.Repeat(caller, count).ToList();
}
