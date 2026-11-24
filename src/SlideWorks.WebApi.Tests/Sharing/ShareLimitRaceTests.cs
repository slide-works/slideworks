using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Tests.Support;
using SlideWorks.WebApi.Tests.Support.Timing;

namespace SlideWorks.WebApi.Tests.Sharing;

// Two shares at the edge of the limit, with the first held inside the guard (inside its write transaction): the second must wait
// on the transaction, then see the first's rows, so exactly one gets through.
[TestFixture]
public class ShareLimitRaceTests : ApiTestFixture
{
    private Gate _gate = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services
        .AddScoped<ShareGuard>()
        .AddScoped<IShareGuard>(provider => new GatedShareGuard(provider.GetRequiredService<ShareGuard>(), _gate));

    [SetUp]
    public void NewGate() => _gate = new Gate();

    [TearDown]
    public void ReleaseGate() => _gate.Release();

    [Test]
    public async Task Put_WhenTwoSharesRace_ShouldNotPassTheLimit()
    {
        var user = TestUser.NewApproved();
        using var first = ClientFor(user);
        using var second = ClientFor(user);
        var deckA = await SharingApi.CreateDeckAsync(first);
        var deckB = await SharingApi.CreateDeckAsync(first);
        var nine = ShareLimitApi.NewEmails(9);
        (await ShareLimitApi.ShareAsync(first, deckA, nine)).StatusCode.ShouldBe(HttpStatusCode.OK);
        _gate.Arm(GatedShareGuard.Key);

        var a = ShareLimitApi.ShareAsync(first, deckA, [.. nine, ShareLimitApi.NewEmails(1)[0]]);
        // Bounded by Gate.DefaultLimit (10 seconds): if the first share never reaches the guard, the test fails with a
        // TimeoutException instead of hanging. Nothing else here waits on the clock; B is held by the transaction, not a sleep.
        await _gate.EnteredAsync();
        var b = ShareLimitApi.ShareAsync(second, deckB, ShareLimitApi.NewEmails(1));
        _gate.Release();
        var statuses = new[] { (await a).StatusCode, (await b).StatusCode };

        statuses.Order().ShouldBe([HttpStatusCode.OK, HttpStatusCode.TooManyRequests]);
        var stored = (await ShareLimitApi.ViewersAsync(first, deckA)).Length + (await ShareLimitApi.ViewersAsync(first, deckB)).Length;
        stored.ShouldBe(10);
    }
}

// The real guard with a Gate in front, so a test can hold one share inside its transaction.
public sealed class GatedShareGuard(ShareGuard inner, Gate gate) : IShareGuard
{
    public const string Key = "guard";

    public async Task AdmitAsync(Guid sharerId, IReadOnlyList<string> newlyAdded)
    {
        await gate.PassAsync(Key);
        await inner.AdmitAsync(sharerId, newlyAdded);
    }
}
