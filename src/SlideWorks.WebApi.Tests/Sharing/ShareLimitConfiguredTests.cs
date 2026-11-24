using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Features.Sharing.Limits;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Sharing;

// The limit comes from SLIDEWORKS_SHARE_NEW_PER_HOUR, and a refusal is logged with the sharer and the limit, never the recipients.
[TestFixture]
public class ShareLimitConfiguredTests : ApiTestFixture
{
    private CapturingLoggerProvider _log = null!;

    protected override Dictionary<string, string?> ExtraConfig => new() { [SharingLimits.NEW_PER_HOUR_KEY] = "2" };

    protected override Action<IServiceCollection>? ConfigureTestServices => services => _log = CapturingLoggerProvider.Replace<ShareGuard>(services);

    private async Task<Guid> SharerIdAsync(TestUser person)
    {
        using var scope = Factory.Services.CreateScope();
        return (await TestPeople.SignInAsync(scope.ServiceProvider, person)).Id;
    }

    [Test]
    public async Task Put_WhenLimitConfigured_ShouldUseIt()
    {
        var person = TestUser.NewApproved();
        using var sharer = ClientFor(person);
        var deck = await SharingApi.CreateDeckAsync(sharer);
        var two = ShareLimitApi.NewEmails(2);
        var third = ShareLimitApi.NewEmails(1)[0];
        (await ShareLimitApi.ShareAsync(sharer, deck, two)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var refused = await ShareLimitApi.ShareAsync(sharer, deck, [.. two, third]);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await ShareLimitApi.ErrorAsync(refused)).ShouldStartWith("You can share with 2 new people an hour. Try again at ");
        var line = _log.Lines.ShouldHaveSingleItem();
        line.ShouldContain("refused: over 2 new people an hour");
        line.ShouldNotContain(third);
        line.ShouldContain($"Sharer {await SharerIdAsync(person)} refused");
    }
}
