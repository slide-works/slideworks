using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Contracts;

// K10 IAgentTokens: a code starts a family; access tokens live 1 hour and are bound to the resource; refresh tokens live
// 30 days and rotate; a reused refresh token or a revoke ends the whole family.
[TestFixture]
public class AgentTokensContractTests : OAuthContractFixture
{
    [Test]
    public async Task Validate_WhenFreshlyIssued_ShouldNameThePresenterAndClient()
    {
        var (client, presenterId, tokens) = await IssueTokensAsync("Claude");

        var agent = await ValidateAsync(tokens.AccessToken);

        agent.ShouldBe(new AgentPrincipal(presenterId, TestUser.Sam.Email, "Claude"));
        client.ClientName.ShouldBe("Claude");
    }

    [Test]
    public async Task Issue_ShouldGiveAnHourLongAccessTokenWithTheGrantedScope()
    {
        var (_, _, tokens) = await IssueTokensAsync();

        tokens.ExpiresInSeconds.ShouldBe(3600);
        tokens.Scope.ShouldBe(SCOPE);
        tokens.RefreshToken.ShouldNotBe(tokens.AccessToken);
        tokens.AccessToken.Length.ShouldBeGreaterThanOrEqualTo(43);
    }

    [Test]
    public async Task Validate_WhenUnknown_ShouldReturnNull()
    {
        (await ValidateAsync("not-a-token")).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenResourceDiffers_ShouldReturnNull()
    {
        var (_, _, tokens) = await IssueTokensAsync();

        (await UseAsync<IAgentTokens, AgentPrincipal?>(t => t.ValidateAsync(tokens.AccessToken, OTHER_RESOURCE))).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenGivenTheRefreshToken_ShouldReturnNull()
    {
        var (_, _, tokens) = await IssueTokensAsync();

        (await ValidateAsync(tokens.RefreshToken)).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenJustBeforeAnHour_ShouldReturnTheAgent()
    {
        var (_, _, tokens) = await IssueTokensAsync();
        Clock.Advance(TimeSpan.FromHours(1) - TimeSpan.FromSeconds(1));

        (await ValidateAsync(tokens.AccessToken)).ShouldNotBeNull();
    }

    [Test]
    public async Task Validate_WhenAnHourHasPassed_ShouldReturnNull()
    {
        var (_, _, tokens) = await IssueTokensAsync();
        Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));

        (await ValidateAsync(tokens.AccessToken)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_ShouldRotateBothTokens()
    {
        var (client, presenterId, tokens) = await IssueTokensAsync();

        var rotated = await RefreshAsync(tokens.RefreshToken, client.ClientId);

        rotated.ShouldNotBeNull();
        rotated.AccessToken.ShouldNotBe(tokens.AccessToken);
        rotated.RefreshToken.ShouldNotBe(tokens.RefreshToken);
        rotated.Scope.ShouldBe(SCOPE);
        (await ValidateAsync(rotated.AccessToken))!.PresenterId.ShouldBe(presenterId);
        (await RefreshAsync(rotated.RefreshToken, client.ClientId)).ShouldNotBeNull();
    }

    [Test]
    public async Task Refresh_WhenAnotherClient_ShouldReturnNull()
    {
        var (_, _, tokens) = await IssueTokensAsync();
        var other = await RegisterClientAsync("Another Agent");

        (await RefreshAsync(tokens.RefreshToken, other.ClientId)).ShouldBeNull();
    }

    // D4: a refresh by the wrong client is refused without spending the token or revoking the family, so it is not a way
    // for another client to knock the owner's agent out.
    [Test]
    public async Task Refresh_WhenAnotherClientTriedFirst_ShouldStillRotateForTheOwner()
    {
        var (client, _, tokens) = await IssueTokensAsync();
        var other = await RegisterClientAsync("Another Agent");
        (await RefreshAsync(tokens.RefreshToken, other.ClientId)).ShouldBeNull();

        (await ValidateAsync(tokens.AccessToken)).ShouldNotBeNull("the family should not be revoked");
        var rotated = await RefreshAsync(tokens.RefreshToken, client.ClientId);

        rotated.ShouldNotBeNull("the refresh token should not be spent");
        (await ValidateAsync(rotated.AccessToken)).ShouldNotBeNull();
    }

    [Test]
    public async Task Refresh_WhenGivenTheAccessToken_ShouldReturnNull()
    {
        var (client, _, tokens) = await IssueTokensAsync();

        (await RefreshAsync(tokens.AccessToken, client.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenJustBeforeThirtyDays_ShouldRotate()
    {
        var (client, _, tokens) = await IssueTokensAsync();
        Clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1));

        (await RefreshAsync(tokens.RefreshToken, client.ClientId)).ShouldNotBeNull();
    }

    [Test]
    public async Task Refresh_WhenThirtyDaysHavePassed_ShouldReturnNull()
    {
        var (client, _, tokens) = await IssueTokensAsync();
        Clock.Advance(TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1));

        (await RefreshAsync(tokens.RefreshToken, client.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenReused_ShouldRevokeTheWholeFamily()
    {
        var (client, _, tokens) = await IssueTokensAsync();
        var rotated = await RefreshAsync(tokens.RefreshToken, client.ClientId);

        (await RefreshAsync(tokens.RefreshToken, client.ClientId)).ShouldBeNull();

        (await ValidateAsync(rotated!.AccessToken)).ShouldBeNull();
        (await RefreshAsync(rotated.RefreshToken, client.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenReused_ShouldLeaveOtherFamiliesAlone()
    {
        var (client, _, tokens) = await IssueTokensAsync();
        var (otherClient, _, otherTokens) = await IssueTokensAsync();
        await RefreshAsync(tokens.RefreshToken, client.ClientId);

        await RefreshAsync(tokens.RefreshToken, client.ClientId);

        (await ValidateAsync(otherTokens.AccessToken)).ShouldNotBeNull();
        (await RefreshAsync(otherTokens.RefreshToken, otherClient.ClientId)).ShouldNotBeNull();
    }

    // One rotation wins. The other 19 present a refresh token that is already used, which is a replay, so the family is
    // revoked, and the winner's new pair with it.
    [Test]
    public async Task Refresh_WhenTwentyRace_ShouldSucceedOnceThenRevokeTheFamily()
    {
        var (client, _, tokens) = await IssueTokensAsync();

        var results = await RaceAsync<IAgentTokens, AgentTokenPair?>(t => t.RefreshAsync(tokens.RefreshToken, client.ClientId));

        var winners = results.Where(r => r != null).ToList();
        winners.Count.ShouldBe(1);
        (await ValidateAsync(winners[0]!.AccessToken)).ShouldBeNull();
        (await RefreshAsync(winners[0]!.RefreshToken, client.ClientId)).ShouldBeNull();
        (await ValidateAsync(tokens.AccessToken)).ShouldBeNull();
    }

    [Test]
    public async Task Revoke_WhenGivenTheAccessToken_ShouldEndTheFamily()
    {
        var (client, _, tokens) = await IssueTokensAsync();

        await UseAsync<IAgentTokens>(t => t.RevokeAsync(tokens.AccessToken));

        (await ValidateAsync(tokens.AccessToken)).ShouldBeNull();
        (await RefreshAsync(tokens.RefreshToken, client.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Revoke_WhenGivenARotatedRefreshToken_ShouldEndTheFamily()
    {
        var (client, _, tokens) = await IssueTokensAsync();
        var rotated = await RefreshAsync(tokens.RefreshToken, client.ClientId);

        await UseAsync<IAgentTokens>(t => t.RevokeAsync(rotated!.RefreshToken));

        (await ValidateAsync(rotated!.AccessToken)).ShouldBeNull();
        (await RefreshAsync(rotated.RefreshToken, client.ClientId)).ShouldBeNull();
    }

    [Test]
    public async Task Revoke_WhenUnknown_ShouldDoNothing()
    {
        var (_, _, tokens) = await IssueTokensAsync();

        await UseAsync<IAgentTokens>(t => t.RevokeAsync("not-a-token"));

        (await ValidateAsync(tokens.AccessToken)).ShouldNotBeNull();
    }

    private Task<AgentPrincipal?> ValidateAsync(string accessToken)
        => UseAsync<IAgentTokens, AgentPrincipal?>(t => t.ValidateAsync(accessToken, Resource));

    private Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId)
        => UseAsync<IAgentTokens, AgentTokenPair?>(t => t.RefreshAsync(refreshToken, clientId));
}
