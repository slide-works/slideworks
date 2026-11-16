using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Contracts;

// K10 IOAuthCodes: single use, 10 minutes, bound to client, redirect URI, PKCE verifier (S256) and resource.
[TestFixture]
public class OAuthCodesContractTests : OAuthContractFixture
{
    [Test]
    public async Task Redeem_WhenEverythingMatches_ShouldReturnTheGrant()
    {
        var (client, presenterId, code) = await IssueAsync();

        var grant = await RedeemAsync(code, client.ClientId);

        grant.ShouldBe(new CodeGrant(client.ClientId, presenterId, SCOPE, Resource));
    }

    [Test]
    public void Challenge_ShouldBeTheS256OfTheVerifier()
    {
        S256(VERIFIER).ShouldBe(CHALLENGE);
    }

    [Test]
    public async Task Issue_WhenTwice_ShouldGiveDifferentUnguessableCodes()
    {
        var (_, _, first) = await IssueAsync();
        var (_, _, second) = await IssueAsync();

        second.ShouldNotBe(first);
        first.Length.ShouldBeGreaterThanOrEqualTo(43);
    }

    [Test]
    public async Task Redeem_WhenUsedTwice_ShouldReturnNullTheSecondTime()
    {
        var (client, _, code) = await IssueAsync();
        (await RedeemAsync(code, client.ClientId)).ShouldNotBeNull();

        (await RedeemAsync(code, client.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenUnknown_ShouldReturnNull()
    {
        var client = await RegisterClientAsync();

        (await RedeemAsync("not-a-code", client.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenVerifierWrong_ShouldReturnNull()
    {
        var (client, _, code) = await IssueAsync();

        (await RedeemAsync(code, client.ClientId, verifier: VERIFIER.Replace('d', 'e'))).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenVerifierIsTheChallenge_ShouldReturnNull()
    {
        var (client, _, code) = await IssueAsync();

        (await RedeemAsync(code, client.ClientId, verifier: CHALLENGE)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenClientDiffers_ShouldReturnNull()
    {
        var (_, _, code) = await IssueAsync();
        var other = await RegisterClientAsync("Another Agent");

        (await RedeemAsync(code, other.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenRedirectDiffers_ShouldReturnNull()
    {
        var (client, _, code) = await IssueAsync();

        (await RedeemAsync(code, client.ClientId, redirect: "http://127.0.0.1:33418/other")).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenResourceDiffers_ShouldReturnNull()
    {
        var (client, _, code) = await IssueAsync();

        (await RedeemAsync(code, client.ClientId, resource: OTHER_RESOURCE)).ShouldBeNull();
    }

    // Any attempt on a live code spends it, so someone holding a stolen code can't keep guessing, and the rightful
    // client learns the code is gone.
    [Test]
    public async Task Redeem_WhenAFailedAttemptCameFirst_ShouldReturnNull()
    {
        var (client, _, code) = await IssueAsync();
        (await RedeemAsync(code, client.ClientId, verifier: VERIFIER.Replace('d', 'e'))).ShouldBeNull();

        (await RedeemAsync(code, client.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenJustBeforeTenMinutes_ShouldReturnTheGrant()
    {
        var (client, _, code) = await IssueAsync();
        Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));

        (await RedeemAsync(code, client.ClientId)).ShouldNotBeNull();
    }

    [Test]
    public async Task Redeem_WhenTenMinutesHavePassed_ShouldReturnNull()
    {
        var (client, _, code) = await IssueAsync();
        Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        (await RedeemAsync(code, client.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenTwentyRace_ShouldSucceedExactlyOnce()
    {
        var (client, _, code) = await IssueAsync();

        var results = await RaceAsync<IOAuthCodes, CodeGrant?>(c => c.RedeemAsync(code, client.ClientId, REDIRECT, VERIFIER, Resource));

        results.Count(r => r != null).ShouldBe(1);
    }

    private async Task<(OAuthClient Client, Guid PresenterId, string Code)> IssueAsync()
    {
        var client = await RegisterClientAsync();
        var sam = await PresenterAsync(TestUser.Sam);
        var code = await UseAsync<IOAuthCodes, string>(c => c.IssueAsync(client.ClientId, sam, REDIRECT, CHALLENGE, SCOPE, Resource));
        return (client, sam, code);
    }

    private Task<CodeGrant?> RedeemAsync(string code, string clientId, string redirect = REDIRECT, string verifier = VERIFIER, string? resource = null)
        => UseAsync<IOAuthCodes, CodeGrant?>(c => c.RedeemAsync(code, clientId, redirect, verifier, resource ?? Resource));
}
