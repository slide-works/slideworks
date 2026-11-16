using SlideWorks.WebApi.Features.Accounts;
using SlideWorks.WebApi.Features.OAuth.Stores;
using SlideWorks.WebApi.Infrastructure.Identity;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.OAuth.Contracts;

// S4 (amends K10): agent tokens follow the presenter's approval. A presenter who is not Approved is refused without
// touching the tokens, and only a revoke kills them for good.
[TestFixture]
public class AgentTokensApprovalContractTests : OAuthContractFixture
{
    [Test]
    public async Task RevokeAllForPresenter_ShouldRevokeEveryFamilyOfThatPresenter()
    {
        var (client, presenterId, first) = await IssueTokensAsync();
        var second = await IssueForAsync(client, presenterId);

        await UseAsync<IAgentTokens>(t => t.RevokeAllForPresenterAsync(presenterId));

        foreach (var tokens in new[] { first, second })
        {
            (await ValidateAsync(tokens.AccessToken)).ShouldBeNull();
            (await RefreshAsync(tokens.RefreshToken, client.ClientId)).ShouldBeNull();
        }
    }

    [Test]
    public async Task RevokeAllForPresenter_ShouldLeaveOtherPresentersAlone()
    {
        var (client, presenterId, _) = await IssueTokensAsync();
        var alexId = await PresenterAsync(TestUser.Alex);
        var alexs = await IssueForAsync(client, alexId);

        await UseAsync<IAgentTokens>(t => t.RevokeAllForPresenterAsync(presenterId));

        (await ValidateAsync(alexs.AccessToken))!.PresenterId.ShouldBe(alexId);
        (await RefreshAsync(alexs.RefreshToken, client.ClientId)).ShouldNotBeNull();
    }

    [Test]
    public async Task RevokeAllForPresenter_WhenNone_ShouldDoNothing()
    {
        var (_, _, tokens) = await IssueTokensAsync();
        var alexId = await PresenterAsync(TestUser.Alex);

        await UseAsync<IAgentTokens>(t => t.RevokeAllForPresenterAsync(alexId));
        await UseAsync<IAgentTokens>(t => t.RevokeAllForPresenterAsync(Guid.NewGuid()));

        (await ValidateAsync(tokens.AccessToken)).ShouldNotBeNull();
    }

    [Test]
    public async Task Validate_WhenPresenterPending_ShouldReturnNull()
    {
        var (client, presenterId) = await PendingPresenterAsync();
        var tokens = await IssueForAsync(client, presenterId);

        (await ValidateAsync(tokens.AccessToken)).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenPresenterRejected_ShouldReturnNull()
    {
        var (_, presenterId, tokens) = await IssueTokensAsync();
        (await ValidateAsync(tokens.AccessToken)).ShouldNotBeNull();

        (await AccountsAsync(a => a.RevokeAsync(presenterId, by: null))).ShouldBe(AccountChange.Changed);

        (await ValidateAsync(tokens.AccessToken)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenPresenterNotApproved_ShouldReturnNullAndKeepTheToken()
    {
        var (client, presenterId, tokens) = await IssueTokensAsync();
        await AccountsAsync(a => a.RevokeAsync(presenterId, by: null));

        (await RefreshAsync(tokens.RefreshToken, client.ClientId)).ShouldBeNull();
        (await RefreshAsync(tokens.RefreshToken, client.ClientId)).ShouldBeNull();

        await AccountsAsync(a => a.ApproveAsync(presenterId, by: null));
        (await ValidateAsync(tokens.AccessToken)).ShouldNotBeNull("a refusal must not revoke the family");
        (await RefreshAsync(tokens.RefreshToken, client.ClientId)).ShouldNotBeNull("a refusal must not spend the refresh token");
    }

    [Test]
    public async Task Refresh_WhenPresenterApprovedAgain_ShouldWork()
    {
        var (client, presenterId) = await PendingPresenterAsync();
        var tokens = await IssueForAsync(client, presenterId);
        (await RefreshAsync(tokens.RefreshToken, client.ClientId)).ShouldBeNull();

        await AccountsAsync(a => a.ApproveAsync(presenterId, by: null));
        var rotated = await RefreshAsync(tokens.RefreshToken, client.ClientId);

        rotated.ShouldNotBeNull();
        (await ValidateAsync(rotated.AccessToken))!.PresenterId.ShouldBe(presenterId);
    }

    private async Task<(OAuthClient Client, Guid PresenterId)> PendingPresenterAsync()
    {
        var presenterId = await PresenterAsync(TestUser.NewPending());
        return (await RegisterClientAsync(), presenterId);
    }

    private Task<AgentTokenPair> IssueForAsync(OAuthClient client, Guid presenterId)
        => UseAsync<IAgentTokens, AgentTokenPair>(t => t.IssueAsync(new CodeGrant(client.ClientId, presenterId, SCOPE, Resource)));

    private Task<AccountChange> AccountsAsync(Func<IUserAccounts, Task<AccountChange>> change)
        => UseAsync<IUserAccounts, AccountChange>(change);

    private Task<AgentPrincipal?> ValidateAsync(string accessToken)
        => UseAsync<IAgentTokens, AgentPrincipal?>(t => t.ValidateAsync(accessToken, Resource));

    private Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId)
        => UseAsync<IAgentTokens, AgentTokenPair?>(t => t.RefreshAsync(refreshToken, clientId));
}
